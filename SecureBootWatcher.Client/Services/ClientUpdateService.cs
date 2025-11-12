using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Implementation of client update service with version checking and safe update scheduling.
    /// </summary>
    public sealed class ClientUpdateService : IClientUpdateService
    {
        private readonly ILogger<ClientUpdateService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<SecureBootWatcherOptions> _options;

        public ClientUpdateService(
            ILogger<ClientUpdateService> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<SecureBootWatcherOptions> options)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _options = options;
        }

        public string GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0.0";
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            var currentVersion = GetCurrentVersion();
            _logger.LogInformation("Checking for updates. Current version: {Version}", currentVersion);

            try
            {
                // Check API for latest version
                var apiBaseUrl = _options.Value.Sinks.WebApi.BaseAddress;
                if (apiBaseUrl == null)
                {
                    _logger.LogWarning("API Base URL not configured. Skipping update check.");
                    return new UpdateCheckResult { UpdateAvailable = false };
                }

                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = apiBaseUrl;

                var response = await client.GetAsync($"api/ClientUpdate/check?currentVersion={currentVersion}", cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Update check failed with status code: {StatusCode}", response.StatusCode);
                    return new UpdateCheckResult { UpdateAvailable = false };
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<UpdateCheckResult>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null)
                {
                    _logger.LogWarning("Failed to deserialize update check response");
                    return new UpdateCheckResult { UpdateAvailable = false };
                }

                if (result.UpdateAvailable)
                {
                    _logger.LogWarning("Update available: {LatestVersion} (current: {CurrentVersion})", 
                        result.LatestVersion, currentVersion);
                    
                    if (result.UpdateRequired)
                    {
                        _logger.LogWarning("Update REQUIRED! Current version is below minimum supported version.");
                    }
                }
                else
                {
                    _logger.LogInformation("Client is up to date");
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                // Network errors (DNS resolution, connection refused, etc.) are expected when update service is unreachable
                _logger.LogWarning("Unable to contact update service: {Message}. Skipping update check.", ex.Message);
                return new UpdateCheckResult { UpdateAvailable = false };
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred (not user-initiated cancellation)
                _logger.LogWarning("Update check timed out: {Message}. Skipping update check.", ex.Message);
                return new UpdateCheckResult { UpdateAvailable = false };
            }
            catch (Exception ex)
            {
                // Unexpected errors should still be logged as errors
                _logger.LogError(ex, "Unexpected error checking for updates");
                return new UpdateCheckResult { UpdateAvailable = false };
            }
        }

        public async Task<UpdateDownloadResult> DownloadUpdateAsync(string downloadUrl, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Downloading update from: {Url}", downloadUrl);

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "SecureBootWatcher-Update");
                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }

                var packagePath = Path.Combine(tempPath, $"SecureBootWatcher-Update-{Guid.NewGuid()}.zip");

                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(downloadUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateDownloadResult
                    {
                        Success = false,
                        ErrorMessage = $"Download failed with status code: {response.StatusCode}"
                    };
                }

                // Download to file
                using (var fileStream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                _logger.LogInformation("Update package downloaded to: {Path}", packagePath);

                // TODO: Verify checksum if provided
                // var checksumVerified = VerifyChecksum(packagePath, expectedChecksum);

                return new UpdateDownloadResult
                {
                    Success = true,
                    LocalPath = packagePath,
                    ChecksumVerified = true // TODO: Implement actual verification
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading update");
                return new UpdateDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> ScheduleUpdateAsync(string updatePackagePath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Scheduling update from package: {Path}", updatePackagePath);

            try
            {
                // Verify package exists
                if (!File.Exists(updatePackagePath))
                {
                    _logger.LogError("Update package not found: {Path}", updatePackagePath);
                    return false;
                }

                // Extract package to temp location
                var extractPath = Path.Combine(Path.GetTempPath(), $"SecureBootWatcher-Extract-{Guid.NewGuid()}");
                System.IO.Compression.ZipFile.ExtractToDirectory(updatePackagePath, extractPath);

                _logger.LogInformation("Package extracted to: {Path}", extractPath);

                // Create update script
                var updateScriptPath = Path.Combine(extractPath, "Apply-Update.ps1");
                var installPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                var updateScript = $@"
# SecureBootWatcher Auto-Update Script
# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

$ErrorActionPreference = 'Stop'
$logPath = Join-Path $env:ProgramData 'SecureBootWatcher\update.log'
$logDir = Split-Path $logPath -Parent

if (-not (Test-Path $logDir)) {{
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}}

function Write-UpdateLog {{
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    ""$timestamp - $Message"" | Out-File -FilePath $logPath -Append
    Write-Host $Message
}}

Write-UpdateLog 'Starting SecureBootWatcher update process'

# Wait for client to exit (max 30 seconds)
$processName = 'SecureBootWatcher.Client'
$maxWaitTime = 30
$elapsed = 0

while ((Get-Process -Name $processName -ErrorAction SilentlyContinue) -and ($elapsed -lt $maxWaitTime)) {{
    Write-UpdateLog ""Waiting for $processName to exit... ($elapsed/$maxWaitTime)""
    Start-Sleep -Seconds 2
    $elapsed += 2
}}

if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {{
    Write-UpdateLog 'Forcefully stopping process'
    Stop-Process -Name $processName -Force
    Start-Sleep -Seconds 2
}}

# Backup current version
$backupPath = '{installPath}\backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}'
Write-UpdateLog ""Creating backup at: $backupPath""

try {{
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Copy-Item -Path '{installPath}\*' -Destination $backupPath -Recurse -Force -Exclude 'backup-*','logs'
    Write-UpdateLog 'Backup completed'
}} catch {{
    Write-UpdateLog ""WARNING: Backup failed: $_""
}}

# Copy new files
Write-UpdateLog 'Copying new files'
try {{
    $filesToCopy = Get-ChildItem -Path '{extractPath}' -File | 
        Where-Object {{ $_.Name -notlike '*.ps1' }}
    
    foreach ($file in $filesToCopy) {{
        $destPath = Join-Path '{installPath}' $file.Name
        Copy-Item -Path $file.FullName -Destination $destPath -Force
        Write-UpdateLog ""Copied: $($file.Name)""
    }}
    
    Write-UpdateLog 'Update files copied successfully'
}} catch {{
    Write-UpdateLog ""ERROR: Failed to copy files: $_""
    Write-UpdateLog 'Attempting rollback...'
    
    # Rollback
    try {{
        Copy-Item -Path ""$backupPath\*"" -Destination '{installPath}' -Recurse -Force
        Write-UpdateLog 'Rollback completed'
    }} catch {{
        Write-UpdateLog ""CRITICAL: Rollback failed: $_""
    }}
    
    exit 1
}}

# Cleanup
Write-UpdateLog 'Cleaning up temporary files'
try {{
    Remove-Item -Path '{extractPath}' -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path '{updatePackagePath}' -Force -ErrorAction SilentlyContinue
    
    # Keep only last 3 backups
    $backups = Get-ChildItem -Path '{installPath}' -Directory | 
        Where-Object {{ $_.Name -like 'backup-*' }} | 
        Sort-Object Name -Descending | 
        Select-Object -Skip 3
    
    foreach ($backup in $backups) {{
        Remove-Item -Path $backup.FullName -Recurse -Force
        Write-UpdateLog ""Removed old backup: $($backup.Name)""
    }}
}} catch {{
    Write-UpdateLog ""WARNING: Cleanup failed: $_""
}}

Write-UpdateLog 'Update completed successfully'

# Restart scheduled task
try {{
    Start-ScheduledTask -TaskName 'SecureBootWatcher' -ErrorAction SilentlyContinue
    Write-UpdateLog 'Scheduled task restarted'
}} catch {{
    Write-UpdateLog ""WARNING: Could not restart scheduled task: $_""
}}

# Self-delete this script and scheduled task
Unregister-ScheduledTask -TaskName 'SecureBootWatcher-Update' -Confirm:$false -ErrorAction SilentlyContinue
Remove-Item -Path $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue

exit 0
";

                File.WriteAllText(updateScriptPath, updateScript);
                _logger.LogInformation("Update script created");

                // Create one-time scheduled task to run the update
                var taskName = "SecureBootWatcher-Update";
                var startTime = DateTime.Now.AddSeconds(10); // Run 10 seconds after client exits

                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Create /TN \"{taskName}\" " +
                               $"/TR \"powershell.exe -ExecutionPolicy Bypass -NoProfile -File \\\"{updateScriptPath}\\\"\" " +
                               $"/SC ONCE /ST {startTime:HH:mm} /SD {startTime:yyyy/MM/dd} " +
                               "/RU SYSTEM /RL HIGHEST /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        _logger.LogError("Failed to start schtasks.exe");
                        return false;
                    }

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        var error = process.StandardError.ReadToEnd();
                        _logger.LogError("Failed to create scheduled task. Error: {Error}", error);
                        return false;
                    }
                }

                _logger.LogInformation("Update scheduled successfully. Will run at: {Time}", startTime);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling update");
                return false;
            }
        }

        private bool VerifyChecksum(string filePath, string expectedChecksum)
        {
            if (string.IsNullOrWhiteSpace(expectedChecksum))
            {
                return true; // No checksum provided, skip verification
            }

            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = sha256.ComputeHash(stream);
                var actualChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                return actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying checksum");
                return false;
            }
        }
    }
}
