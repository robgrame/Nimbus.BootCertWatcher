using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    internal sealed class ReportBuilder : IReportBuilder
    {
        private readonly ILogger<ReportBuilder> _logger;
        private readonly IRegistrySnapshotProvider _registrySnapshotProvider;
        private readonly IEventLogReader _eventLogReader;
        private readonly ISecureBootCertificateEnumerator _certificateEnumerator;
        private readonly IClientUpdateService? _updateService;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;

        public ReportBuilder(
            ILogger<ReportBuilder> logger,
            IRegistrySnapshotProvider registrySnapshotProvider,
            IEventLogReader eventLogReader,
            ISecureBootCertificateEnumerator certificateEnumerator,
            IOptionsMonitor<SecureBootWatcherOptions> options,
            IClientUpdateService? updateService = null)
        {
            _logger = logger;
            _registrySnapshotProvider = registrySnapshotProvider;
            _eventLogReader = eventLogReader;
            _certificateEnumerator = certificateEnumerator;
            _options = options;
            _updateService = updateService;
        }

        public async Task<SecureBootStatusReport> BuildAsync(CancellationToken cancellationToken)
        {
            var registrySnapshot = await _registrySnapshotProvider.CaptureAsync(cancellationToken).ConfigureAwait(false);
            var deviceAttributesSnapshot = await _registrySnapshotProvider.CaptureDeviceAttributesAsync(cancellationToken).ConfigureAwait(false);
            var recentEvents = await _eventLogReader.ReadRecentEventsAsync(cancellationToken).ConfigureAwait(false);

            // Enumerate certificates
            SecureBootCertificateCollection? certificates = null;
            try
            {
                certificates = await _certificateEnumerator.EnumerateAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enumerate Secure Boot certificates. Report will continue without certificate details.");
            }

            // Check for client updates
            UpdateCheckResult? updateCheck = null;
            if (_updateService != null && _options.CurrentValue.ClientUpdate.CheckForUpdates)
            {
                try
                {
                    updateCheck = await _updateService.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check for client updates");
                }
            }

            var report = new SecureBootStatusReport
            {
                Device = BuildDeviceIdentity(),
                Registry = registrySnapshot,
                DeviceAttributes = deviceAttributesSnapshot,
                Certificates = certificates,
                Events = recentEvents.ToList(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ClientVersion = GetClientVersion(),
                CorrelationId = Guid.NewGuid().ToString("N")
            };

            PopulateAlerts(report, updateCheck);

            // Handle auto-download if enabled
            if (updateCheck?.UpdateAvailable == true && 
                _options.CurrentValue.ClientUpdate.AutoDownloadEnabled &&
                _updateService != null)
            {
                await HandleAutoDownloadAsync(updateCheck, cancellationToken);
            }

            return report;
        }

        private static string GetClientVersion()
        {
            // Try to get version from AssemblyInformationalVersionAttribute first (GitVersioning)
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                // Remove commit hash (everything after '+') if present
                // Example: "1.1.1.48182+a1b2c3d" -> "1.1.1.48182"
                var plusIndex = informationalVersion.IndexOf('+');
                if (plusIndex > 0)
                {
                    return informationalVersion.Substring(0, plusIndex);
                }
                
                return informationalVersion;
            }
            
            // Fallback to AssemblyVersion
            var version = assembly.GetName().Version;
            if (version != null)
            {
                return version.ToString();
            }
            
            // Final fallback
            return "1.0.0.0";
        }

        private DeviceIdentity BuildDeviceIdentity()
        {
            var identity = new DeviceIdentity
            {
                DomainName = Environment.UserDomainName,
                UserPrincipalName = Environment.UserName,
                ClientVersion = GetClientVersion()
            };

            var options = _options.CurrentValue;
            if (!string.IsNullOrWhiteSpace(options.FleetId))
            {
                identity.Tags["FleetId"] = options.FleetId!;
            }

            TryPopulateHardwareInfo(identity);

            return identity;
        }

        private void TryPopulateHardwareInfo(DeviceIdentity identity)
        {
            // Try to get computer system information
            try
            {
                using var systemSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
                using var systemCollection = systemSearcher.Get();
                
                foreach (ManagementObject system in systemCollection)
                {
                    try
                    {
                        identity.Manufacturer = system["Manufacturer"]?.ToString();
                        identity.Model = system["Model"]?.ToString();
                    }
                    catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
                    {
                        _logger.LogDebug("Win32_ComputerSystem property not found: {Message}", ex.Message);
                    }
                    finally
                    {
                        system?.Dispose();
                    }
                    break; // Only process first result
                }
            }
            catch (ManagementException ex)
            {
                _logger.LogDebug(ex, "Failed to query Win32_ComputerSystem. Manufacturer and Model will be unavailable.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error querying Win32_ComputerSystem.");
            }

            // Try to get BIOS information
            try
            {
                using var biosSearcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
                using var biosCollection = biosSearcher.Get();
                
                foreach (ManagementObject bios in biosCollection)
                {
                    try
                    {
                        identity.FirmwareVersion = bios["SMBIOSBIOSVersion"]?.ToString();
                        
                        var releaseDate = bios["ReleaseDate"]?.ToString();
                        if (!string.IsNullOrEmpty(releaseDate))
                        {
                            try
                            {
                                identity.FirmwareReleaseDate = ManagementDateTimeConverter.ToDateTime(releaseDate);
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                _logger.LogDebug("Invalid ReleaseDate format from Win32_BIOS: {ReleaseDate}", releaseDate);
                            }
                        }
                    }
                    catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
                    {
                        _logger.LogDebug("Win32_BIOS property not found: {Message}", ex.Message);
                    }
                    finally
                    {
                        bios?.Dispose();
                    }
                    break; // Only process first result
                }
            }
            catch (ManagementException ex)
            {
                _logger.LogDebug(ex, "Failed to query Win32_BIOS. Firmware information will be unavailable.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error querying Win32_BIOS.");
            }

            // Enrich with OS information
            DeviceIdentityEnricher.EnrichWithOSInfo(identity, _logger);

            // Enrich with Chassis information
            DeviceIdentityEnricher.EnrichWithChassisInfo(identity, _logger);

            // Detect Virtual Machine
            DeviceIdentityEnricher.DetectVirtualMachine(identity, _logger);
            
            // Log summary of what was collected
            _logger.LogDebug(
                "Hardware info collected: Manufacturer={Manufacturer}, Model={Model}, FirmwareVersion={FirmwareVersion}, OS={OS}, OSVersion={OSVersion}, OSProductType={OSProductType}, ChassisTypes={ChassisTypes}, IsVM={IsVM}, Platform={Platform}",
                identity.Manufacturer ?? "N/A",
                identity.Model ?? "N/A", 
                identity.FirmwareVersion ?? "N/A",
                identity.OperatingSystem ?? "N/A",
                identity.OSVersion ?? "N/A",
                identity.OSProductType?.ToString() ?? "N/A",
                identity.ChassisTypes != null ? string.Join(",", identity.ChassisTypes) : "N/A",
                identity.IsVirtualMachine?.ToString() ?? "N/A",
                identity.VirtualizationPlatform ?? "N/A");
        }

        private void PopulateAlerts(SecureBootStatusReport report, UpdateCheckResult? updateCheck)
        {
            var alerts = new List<string>();

            if (report.Registry.UefiCa2023Status == SecureBootDeploymentState.Error)
            {
                alerts.Add($"Secure Boot update reported error code {report.Registry.UefiCa2023Error ?? 0}.");
            }

            if (report.Registry.UefiCa2023Status == SecureBootDeploymentState.NotStarted)
            {
                alerts.Add("Secure Boot certificate update has not started on this device.");
            }

            if (report.Registry.HighConfidenceOptOut == true)
            {
                alerts.Add("Device is opted out of high-confidence automatic deployments.");
            }

            if (report.Registry.MicrosoftUpdateManagedOptIn == true)
            {
                alerts.Add("Device is opted in to Microsoft managed deployment (CFR).");
            }

            if (report.Events.Count == 0 && report.Registry.UefiCa2023Status != SecureBootDeploymentState.Updated)
            {
                alerts.Add("No Secure Boot events detected within the lookback window.");
            }

            if (report.Registry.AvailableUpdates.HasValue)
            {
                var progressionState = SecureBootUpdateFlagsExtensions.GetProgressionState(report.Registry.AvailableUpdates);
                var completionPercentage = SecureBootUpdateFlagsExtensions.GetCompletionPercentage(report.Registry.AvailableUpdates);
                
                alerts.Add($"Deployment Progress: {progressionState} ({completionPercentage}% complete)");

                var pendingFlags = SecureBootUpdateFlagsExtensions.GetActiveFlags(report.Registry.AvailableUpdates);
                if (pendingFlags.Count > 0)
                {
                    alerts.Add($"Pending updates: {string.Join(", ", pendingFlags)}");
                }
            }

            if (report.Certificates != null)
            {
                if (report.Certificates.SecureBootEnabled == false)
                {
                    alerts.Add("Secure Boot is not enabled on this device.");
                }

                if (report.Certificates.ExpiredCertificateCount > 0)
                {
                    alerts.Add($"{report.Certificates.ExpiredCertificateCount} expired certificate(s) detected in Secure Boot databases.");
                }

                if (report.Certificates.ExpiringCertificateCount > 0)
                {
                    alerts.Add($"{report.Certificates.ExpiringCertificateCount} certificate(s) expiring within 90 days.");
                }

                if (!string.IsNullOrEmpty(report.Certificates.ErrorMessage))
                {
                    alerts.Add($"Certificate enumeration error: {report.Certificates.ErrorMessage}");
                }
            }

            // Add client update alerts
            if (updateCheck != null && _options.CurrentValue.ClientUpdate.NotifyOnUpdateAvailable)
            {
                if (updateCheck.UpdateRequired)
                {
                    alerts.Add($"?? CLIENT UPDATE REQUIRED: Version {updateCheck.LatestVersion} is available (current: {updateCheck.CurrentVersion}). Update is mandatory.");
                }
                else if (updateCheck.UpdateAvailable)
                {
                    alerts.Add($"?? Client update available: Version {updateCheck.LatestVersion} (current: {updateCheck.CurrentVersion})");
                }
            }

            foreach (var alert in alerts)
            {
                report.Alerts.Add(alert);
            }
        }

        private async Task HandleAutoDownloadAsync(UpdateCheckResult updateCheck, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(updateCheck.DownloadUrl) || _updateService == null)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Auto-download enabled. Downloading update from: {Url}", updateCheck.DownloadUrl);
                
                var downloadResult = await _updateService.DownloadUpdateAsync(updateCheck.DownloadUrl, cancellationToken);
                
                if (!downloadResult.Success)
                {
                    _logger.LogWarning("Failed to download update: {Error}", downloadResult.ErrorMessage);
                    return;
                }

                _logger.LogInformation("Update downloaded successfully to: {Path}", downloadResult.LocalPath);

                // If auto-install is enabled, schedule the update
                if (_options.CurrentValue.ClientUpdate.AutoInstallEnabled && !string.IsNullOrWhiteSpace(downloadResult.LocalPath))
                {
                    _logger.LogInformation("Auto-install enabled. Scheduling update...");
                    
                    var scheduled = await _updateService.ScheduleUpdateAsync(downloadResult.LocalPath, cancellationToken);
                    
                    if (scheduled)
                    {
                        _logger.LogInformation("Update scheduled successfully. Will be applied after current execution completes.");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to schedule update");
                    }
                }
                else
                {
                    _logger.LogInformation("Auto-install disabled. Update downloaded but not scheduled. Manual installation required.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling auto-download");
            }
        }
    }
}
