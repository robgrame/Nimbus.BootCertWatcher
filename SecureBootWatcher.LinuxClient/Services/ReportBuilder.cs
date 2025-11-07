using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Services
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
                DomainName = Environment.UserDomainName ?? Environment.MachineName,
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
                // On Linux, read hardware info from /sys/class/dmi/id/
                var dmiPath = "/sys/class/dmi/id";
                
                if (Directory.Exists(dmiPath))
                {
                    identity.Manufacturer = TryReadFile(Path.Combine(dmiPath, "sys_vendor"))?.Trim();
                    identity.Model = TryReadFile(Path.Combine(dmiPath, "product_name"))?.Trim();
                    identity.FirmwareVersion = TryReadFile(Path.Combine(dmiPath, "bios_version"))?.Trim();
                    
                    _logger.LogDebug("Hardware info: Manufacturer={Manufacturer}, Model={Model}, Firmware={Firmware}",
                        identity.Manufacturer, identity.Model, identity.FirmwareVersion);
                }
                else
                {
                    _logger.LogWarning("DMI info path {Path} not found. Hardware metadata will be incomplete.", dmiPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to populate hardware metadata for Secure Boot report.");
            }
        }

        private string? TryReadFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to read file {Path}", path);
            }
            
            return null;
        }

        private static void PopulateAlerts(SecureBootStatusReport report)
        {
            var alerts = new List<string>();

            if (report.Registry.DeploymentState == SecureBootDeploymentState.Error)
            {
                alerts.Add($"Secure Boot update reported error code {report.Registry.UefiCa2023ErrorCode ?? 0}.");
            }

            if (report.Registry.DeploymentState == SecureBootDeploymentState.NotStarted)
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

            if (report.Events.Count == 0 && report.Registry.DeploymentState != SecureBootDeploymentState.Updated)
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
