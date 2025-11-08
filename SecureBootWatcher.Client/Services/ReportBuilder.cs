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
        private readonly IOptionsMonitor<SecureBootWatcherOptions> _options;

        public ReportBuilder(
            ILogger<ReportBuilder> logger,
            IRegistrySnapshotProvider registrySnapshotProvider,
            IEventLogReader eventLogReader,
            ISecureBootCertificateEnumerator certificateEnumerator,
            IOptionsMonitor<SecureBootWatcherOptions> options)
        {
            _logger = logger;
            _registrySnapshotProvider = registrySnapshotProvider;
            _eventLogReader = eventLogReader;
            _certificateEnumerator = certificateEnumerator;
            _options = options;
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

            var report = new SecureBootStatusReport
            {
                Device = BuildDeviceIdentity(),
                Registry = registrySnapshot,
                DeviceAttributes = deviceAttributesSnapshot, // Include device attributes snapshot
                Certificates = certificates,
                Events = recentEvents.ToList(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ClientVersion = GetClientVersion(),
                CorrelationId = Guid.NewGuid().ToString("N")
            };

            PopulateAlerts(report);

            return report;
        }

        private static string GetClientVersion()
        {
            // Try to get version from AssemblyInformationalVersionAttribute first (GitVersioning)
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
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
            
            // Log summary of what was collected
            _logger.LogDebug(
                "Hardware info collected: Manufacturer={Manufacturer}, Model={Model}, FirmwareVersion={FirmwareVersion}",
                identity.Manufacturer ?? "N/A",
                identity.Model ?? "N/A", 
                identity.FirmwareVersion ?? "N/A");
        }

        private static void PopulateAlerts(SecureBootStatusReport report)
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

            // Add AvailableUpdates progression information
            if (report.Registry.AvailableUpdates.HasValue)
            {
                var progressionState = SecureBootUpdateFlagsExtensions.GetProgressionState(report.Registry.AvailableUpdates);
                var completionPercentage = SecureBootUpdateFlagsExtensions.GetCompletionPercentage(report.Registry.AvailableUpdates);
                
                alerts.Add($"Deployment Progress: {progressionState} ({completionPercentage}% complete)");

                // Add pending updates information
                var pendingFlags = SecureBootUpdateFlagsExtensions.GetActiveFlags(report.Registry.AvailableUpdates);
                if (pendingFlags.Count > 0)
                {
                    alerts.Add($"Pending updates: {string.Join(", ", pendingFlags)}");
                }
            }

            // Add certificate-related alerts
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

            foreach (var alert in alerts)
            {
                report.Alerts.Add(alert);
            }
        }
    }
}
