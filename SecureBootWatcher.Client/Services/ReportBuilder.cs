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
            try
            {
                using var systemSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
                foreach (var system in systemSearcher.Get())
                {
                    identity.Manufacturer = system["Manufacturer"]?.ToString();
                    identity.Model = system["Model"]?.ToString();
                    break;
                }

                using var biosSearcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
                foreach (var bios in biosSearcher.Get())
                {
                    identity.FirmwareVersion = bios["SMBIOSBIOSVersion"]?.ToString();
                    identity.FirmwareReleaseDate = ManagementDateTimeConverter.ToDateTime(bios["ReleaseDate"]?.ToString() ?? string.Empty);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to populate hardware metadata for Secure Boot report.");
            }
        }

        private static void PopulateAlerts(SecureBootStatusReport report)
        {
            var alerts = new List<string>();

            if (report.Registry.UefiCa2023Status == SecureBootDeploymentState.Error)
            {
                alerts.Add("Secure Boot update reported an error."); // Adjusted message
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
