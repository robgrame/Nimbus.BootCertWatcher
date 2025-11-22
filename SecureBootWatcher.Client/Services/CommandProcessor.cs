using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Processes device configuration commands from the API.
    /// Handles command fetching, execution, verification, and result reporting.
    /// </summary>
    internal sealed class CommandProcessor : ICommandProcessor
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CommandProcessor> _logger;
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;
        private readonly IRegistrySnapshotProvider _registryProvider;

        public CommandProcessor(
            IHttpClientFactory httpClientFactory,
            ILogger<CommandProcessor> logger,
            IOptionsMonitor<SecureBootWatcherOptions> options,
            IRegistrySnapshotProvider registryProvider)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _options = options;
            _registryProvider = registryProvider;
        }

        public async Task<IReadOnlyList<DeviceConfigurationCommand>> FetchPendingCommandsAsync(CancellationToken cancellationToken)
        {
            // Check if command processing is enabled
            if (!_options.CurrentValue.Commands.EnableCommandProcessing)
            {
                _logger.LogDebug("Command processing is disabled in configuration");
                return Array.Empty<DeviceConfigurationCommand>();
            }

            var apiBaseUrl = _options.CurrentValue.Sinks.WebApi.BaseAddress;
            if (apiBaseUrl == null || string.IsNullOrEmpty(apiBaseUrl.ToString()))
            {
                _logger.LogWarning("WebApi BaseAddress not configured, cannot fetch commands");
                return Array.Empty<DeviceConfigurationCommand>();
            }

            try
            {
                var httpClient = _httpClientFactory.CreateClient("SecureBootIngestion");
                
                // TODO: Replace with actual device ID (from Machine GUID or similar)
                var deviceId = GetDeviceIdentifier();
                
                var requestUri = $"{apiBaseUrl.ToString().TrimEnd('/')}/api/ClientCommands/pending?deviceId={deviceId}";
                
                _logger.LogDebug("Fetching pending commands from: {Uri}", requestUri);
                
                var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch pending commands. Status: {StatusCode}", response.StatusCode);
                    return Array.Empty<DeviceConfigurationCommand>();
                }
                
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var commands = JsonSerializer.Deserialize<List<DeviceConfigurationCommand>>(json) 
                               ?? new List<DeviceConfigurationCommand>();
                
                _logger.LogInformation("Fetched {Count} pending command(s)", commands.Count);
                
                return commands;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch pending commands from API");
                return Array.Empty<DeviceConfigurationCommand>();
            }
        }

        public async Task<DeviceConfigurationResult> ExecuteCommandAsync(
            DeviceConfigurationCommand command, 
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Executing command {CommandId} of type {Type}", 
                command.CommandId, 
                command.ConfigurationType);

            try
            {
                DeviceConfigurationResult result;

                switch (command.ConfigurationType)
                {
                    case DeviceConfigurationType.CertificateUpdate:
                        result = await ExecuteCertificateUpdateAsync((CertificateUpdateCommand)command, cancellationToken);
                        break;

                    case DeviceConfigurationType.MicrosoftUpdateOptIn:
                        result = await ExecuteMicrosoftUpdateOptInAsync((MicrosoftUpdateOptInCommand)command, cancellationToken);
                        break;

                    case DeviceConfigurationType.TelemetryConfiguration:
                        result = await ExecuteTelemetryConfigAsync((TelemetryConfigurationCommand)command, cancellationToken);
                        break;

                    default:
                        result = new DeviceConfigurationResult
                        {
                            CommandId = command.CommandId,
                            DeviceId = Guid.Parse(GetDeviceIdentifier()),
                            Success = false,
                            Message = $"Unsupported command type: {command.ConfigurationType}"
                        };
                        break;
                }

                _logger.LogInformation(
                    "Command {CommandId} execution completed. Success: {Success}, Message: {Message}",
                    command.CommandId,
                    result.Success,
                    result.Message);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute command {CommandId}", command.CommandId);
                
                return new DeviceConfigurationResult
                {
                    CommandId = command.CommandId,
                    DeviceId = Guid.Parse(GetDeviceIdentifier()),
                    Success = false,
                    Message = $"Exception during execution: {ex.Message}"
                };
            }
        }

        public async Task<DeviceConfigurationState> VerifyCommandResultAsync(
            DeviceConfigurationCommand command, 
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Verifying command {CommandId} result", command.CommandId);

            try
            {
                // Capture current state from registry
                var registrySnapshot = await _registryProvider.CaptureAsync(cancellationToken);
                var telemetrySnapshot = await _registryProvider.CaptureTelemetryPolicyAsync(cancellationToken);

                return new DeviceConfigurationState
                {
                    MicrosoftUpdateManagedOptIn = registrySnapshot.MicrosoftUpdateManagedOptIn,
                    AllowTelemetry = telemetrySnapshot?.AllowTelemetry,
                    WindowsUEFICA2023Capable = registrySnapshot.WindowsUEFICA2023CapableCode,
                    SnapshotTimestampUtc = DateTimeOffset.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to verify command result for {CommandId}", command.CommandId);
                return new DeviceConfigurationState();
            }
        }

        public async Task<bool> ReportResultAsync(DeviceConfigurationResult result, CancellationToken cancellationToken)
        {
            var apiBaseUrl = _options.CurrentValue.Sinks.WebApi.BaseAddress;
            if (apiBaseUrl == null || string.IsNullOrEmpty(apiBaseUrl.ToString()))
            {
                _logger.LogWarning("WebApi BaseAddress not configured, cannot report result");
                return false;
            }

            try
            {
                var httpClient = _httpClientFactory.CreateClient("SecureBootIngestion");
                
                var requestUri = $"{apiBaseUrl.ToString().TrimEnd('/')}/api/ClientCommands/result";
                
                var json = JsonSerializer.Serialize(result);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Reporting command result to: {Uri}", requestUri);
                
                var response = await httpClient.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully reported result for command {CommandId}", result.CommandId);
                    return true;
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to report result for command {CommandId}. Status: {StatusCode}", 
                        result.CommandId, 
                        response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to report result for command {CommandId}", result.CommandId);
                return false;
            }
        }

        // ===== Private Helper Methods =====

        private async Task<DeviceConfigurationResult> ExecuteCertificateUpdateAsync(
            CertificateUpdateCommand command, 
            CancellationToken cancellationToken)
        {
            var deviceId = Guid.Parse(GetDeviceIdentifier());

            try
            {
                const string registryPath = @"SYSTEM\CurrentControlSet\Control\SecureBoot";

                using var key = Registry.LocalMachine.OpenSubKey(registryPath, writable: true);
                if (key == null)
                {
                    return FailureResult(command.CommandId, deviceId, "Cannot open SecureBoot registry key (requires admin privileges)");
                }

                // Set UpdateType registry value
                var updateTypeValue = command.UpdateType ?? 1; // Default to DB update (1)
                key.SetValue("UpdateType", updateTypeValue, RegistryValueKind.DWord);

                _logger.LogInformation("Set UpdateType to {Value} in registry", updateTypeValue);

                // Verify the change
                var verifiedValue = key.GetValue("UpdateType");
                if (verifiedValue == null || (uint)verifiedValue != updateTypeValue)
                {
                    return FailureResult(command.CommandId, deviceId, "UpdateType registry value not set correctly");
                }

                // Capture post-execution state
                var currentState = await VerifyCommandResultAsync(command, cancellationToken);

                return new DeviceConfigurationResult
                {
                    CommandId = command.CommandId,
                    DeviceId = deviceId,
                    Success = true,
                    Message = $"UpdateType set to {updateTypeValue}. Change verified in registry.",
                    CurrentState = currentState
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied writing to registry for command {CommandId}", command.CommandId);
                return FailureResult(command.CommandId, deviceId, "Access denied. Client must run with Administrator privileges.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute certificate update command {CommandId}", command.CommandId);
                return FailureResult(command.CommandId, deviceId, $"Execution failed: {ex.Message}");
            }
        }

        private async Task<DeviceConfigurationResult> ExecuteMicrosoftUpdateOptInAsync(
            MicrosoftUpdateOptInCommand command, 
            CancellationToken cancellationToken)
        {
            var deviceId = Guid.Parse(GetDeviceIdentifier());

            try
            {
                const string registryPath = @"SYSTEM\CurrentControlSet\Control\SecureBoot";

                using var key = Registry.LocalMachine.OpenSubKey(registryPath, writable: true);
                if (key == null)
                {
                    return FailureResult(command.CommandId, deviceId, "Cannot open SecureBoot registry key (requires admin privileges)");
                }

                // Set MicrosoftUpdateManagedOptIn registry value
                var optInValue = command.OptIn ? 1 : 0;
                key.SetValue("MicrosoftUpdateManagedOptIn", optInValue, RegistryValueKind.DWord);

                _logger.LogInformation("Set MicrosoftUpdateManagedOptIn to {Value} ({State})", optInValue, command.OptIn ? "Enabled" : "Disabled");

                // Verify the change
                var verifiedValue = key.GetValue("MicrosoftUpdateManagedOptIn");
                if (verifiedValue == null || (int)verifiedValue != optInValue)
                {
                    return FailureResult(command.CommandId, deviceId, "MicrosoftUpdateManagedOptIn registry value not set correctly");
                }

                // Capture post-execution state
                var currentState = await VerifyCommandResultAsync(command, cancellationToken);

                return new DeviceConfigurationResult
                {
                    CommandId = command.CommandId,
                    DeviceId = deviceId,
                    Success = true,
                    Message = $"MicrosoftUpdateManagedOptIn set to {(command.OptIn ? "Enabled" : "Disabled")}. Change verified in registry.",
                    CurrentState = currentState
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied writing to registry for command {CommandId}", command.CommandId);
                return FailureResult(command.CommandId, deviceId, "Access denied. Client must run with Administrator privileges.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Microsoft Update opt-in command {CommandId}", command.CommandId);
                return FailureResult(command.CommandId, deviceId, $"Execution failed: {ex.Message}");
            }
        }

        private async Task<DeviceConfigurationResult> ExecuteTelemetryConfigAsync(
            TelemetryConfigurationCommand command, 
            CancellationToken cancellationToken)
        {
            var deviceId = Guid.Parse(GetDeviceIdentifier());

            try
            {
                const string registryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection";

                // For validation-only mode
                if (command.ValidateOnly)
                {
                    using var readKey = Registry.LocalMachine.OpenSubKey(registryPath);
                    var currentValue = readKey?.GetValue("AllowTelemetry") as int? ?? 0;
                    
                    bool meetsRequirement = currentValue >= command.RequiredTelemetryLevel;

                    var telemetryLevelName = GetTelemetryLevelName((uint)currentValue);
                    var requiredLevelName = GetTelemetryLevelName(command.RequiredTelemetryLevel);

                    var validationState = await VerifyCommandResultAsync(command, cancellationToken);

                    return new DeviceConfigurationResult
                    {
                        CommandId = command.CommandId,
                        DeviceId = deviceId,
                        Success = meetsRequirement,
                        Message = meetsRequirement
                            ? $"Telemetry validation passed. Current: {telemetryLevelName} ({currentValue}), Required: {requiredLevelName} ({command.RequiredTelemetryLevel})"
                            : $"Telemetry validation failed. Current: {telemetryLevelName} ({currentValue}), Required: {requiredLevelName} ({command.RequiredTelemetryLevel})",
                        CurrentState = validationState
                    };
                }

                // Configuration mode - actually set the telemetry level
                using var key = Registry.LocalMachine.OpenSubKey(registryPath, writable: true);
                if (key == null)
                {
                    return FailureResult(command.CommandId, deviceId, "Cannot open DataCollection registry key (requires admin privileges)");
                }

                // Set AllowTelemetry registry value
                key.SetValue("AllowTelemetry", command.RequiredTelemetryLevel, RegistryValueKind.DWord);

                var levelName = GetTelemetryLevelName(command.RequiredTelemetryLevel);
                _logger.LogInformation("Set AllowTelemetry to {Value} ({LevelName})", command.RequiredTelemetryLevel, levelName);

                // Verify the change
                var verifiedValue = key.GetValue("AllowTelemetry");
                if (verifiedValue == null || (int)verifiedValue != command.RequiredTelemetryLevel)
                {
                    return FailureResult(command.CommandId, deviceId, "AllowTelemetry registry value not set correctly");
                }

                // Capture post-execution state
                var currentState = await VerifyCommandResultAsync(command, cancellationToken);

                return new DeviceConfigurationResult
                {
                    CommandId = command.CommandId,
                    DeviceId = deviceId,
                    Success = true,
                    Message = $"AllowTelemetry set to {levelName} ({command.RequiredTelemetryLevel}). Change verified in registry.",
                    CurrentState = currentState
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied writing to registry for command {CommandId}", command.CommandId);
                return FailureResult(command.CommandId, deviceId, "Access denied. Client must run with Administrator privileges.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute telemetry configuration command {CommandId}", command.CommandId);
                return FailureResult(command.CommandId, deviceId, $"Execution failed: {ex.Message}");
            }
        }

        private static string GetTelemetryLevelName(uint level)
        {
            return level switch
            {
                0 => "Security",
                1 => "Basic",
                2 => "Enhanced",
                3 => "Full",
                _ => $"Unknown ({level})"
            };
        }

        private static DeviceConfigurationResult FailureResult(Guid commandId, Guid deviceId, string message)
        {
            return new DeviceConfigurationResult
            {
                CommandId = commandId,
                DeviceId = deviceId,
                Success = false,
                Message = message
            };
        }

        private static string GetDeviceIdentifier()
        {
            // Use machine name + domain as unique identifier
            // In production, you might want to use a more stable identifier like Machine GUID
            var machineName = Environment.MachineName;
            var domain = Environment.UserDomainName;
            
            // Create a deterministic GUID from machine name and domain
            var identifierString = $"{domain}\\{machineName}".ToLowerInvariant();
            var hashBytes = System.Security.Cryptography.MD5.Create().ComputeHash(
                Encoding.UTF8.GetBytes(identifierString));
            
            return new Guid(hashBytes).ToString();
        }
    }
}
