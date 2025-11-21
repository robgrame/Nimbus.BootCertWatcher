using System;
using System.Globalization;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    internal sealed class RegistrySnapshotProvider : IRegistrySnapshotProvider
    {
        private readonly ILogger<RegistrySnapshotProvider> _logger;

        public RegistrySnapshotProvider(ILogger<RegistrySnapshotProvider> logger)
        {
            _logger = logger;
        }

        public Task<SecureBootRegistrySnapshot> CaptureAsync(CancellationToken cancellationToken)
        {
            var snapshot = new SecureBootRegistrySnapshot
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(SecureBootRegistrySnapshot.RegistryRootPath, false);
                if (baseKey == null)
                {
                    _logger.LogDebug("Secure Boot base registry path not found at {Path}. This is normal for devices without Secure Boot servicing configured.", SecureBootRegistrySnapshot.RegistryRootPath);
                    return Task.FromResult(snapshot);
                }

                snapshot.AvailableUpdates = ReadUInt(baseKey, "AvailableUpdates");
                snapshot.UpdateType = ReadUInt(baseKey, "UpdateType");
                snapshot.HighConfidenceOptOut = ReadBool(baseKey, "HighConfidenceOptOut");
                snapshot.MicrosoftUpdateManagedOptIn = ReadBool(baseKey, "MicrosoftUpdateManagedOptIn");

                using var servicingKey = baseKey.OpenSubKey("Servicing", false);
                if (servicingKey != null)
                {
                    snapshot.UefiCa2023Status = (SecureBootDeploymentState?)ReadUInt(servicingKey, "UEFICA2023Status") ?? SecureBootDeploymentState.Unknown;
                    snapshot.UefiCa2023Error = ReadUInt(servicingKey, "UefiCa2023Error");
                    snapshot.WindowsUEFICA2023CapableCode = ReadUInt(servicingKey, "WindowsUEFICA2023CapableCode");
                    
                    snapshot.UefiCa2024Status = (SecureBootDeploymentState?)ReadUInt(servicingKey, "UEFICA2024Status") ?? SecureBootDeploymentState.Unknown;
                    snapshot.UefiCa2024Error = ReadUInt(servicingKey, "UEFICA2024Error");
                }
                else
                {
                    _logger.LogDebug("Servicing subkey not found. Device may not have Secure Boot servicing registry keys.");
                }

                using var stateKey = baseKey.OpenSubKey("State", false);
                if (stateKey != null)
                {
                    snapshot.PolicyPublisher = ReadString(stateKey, "PolicyPublisher");
                    snapshot.PolicyVersion = ReadUInt(stateKey, "PolicyVersion");
                    snapshot.UEFISecureBootEnabled = ReadBool(stateKey, "UEFISecureBootEnabled");
                }
                else
                {
                    _logger.LogDebug("State subkey not found. UEFI Secure Boot status will be unavailable.");
                }
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, "Access denied reading Secure Boot registry keys. Run as Administrator or check permissions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while reading Secure Boot registry keys.");
            }

            return Task.FromResult(snapshot);
        }

        public Task<SecureBootDeviceAttributesRegistrySnapshot> CaptureDeviceAttributesAsync(CancellationToken cancellationToken)
        {
            var snapshot = new SecureBootDeviceAttributesRegistrySnapshot
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(SecureBootDeviceAttributesRegistrySnapshot.RegistryRootPath, false);
                if (baseKey == null)
                {
                    _logger.LogDebug("Device Attributes registry path not found at {Path}. This is normal for devices without Secure Boot servicing configured.", SecureBootDeviceAttributesRegistrySnapshot.RegistryRootPath);
                    return Task.FromResult(snapshot);
                }

                snapshot.CanAttemptUpdateAfter = ReadDateTimeOffset(baseKey, "CanAttemptUpdateAfter");
                snapshot.OEMManufacturerName = ReadString(baseKey, "OEMManufacturerName");
                snapshot.OEMModelSystemVersion = ReadString(baseKey, "OEMModelSystemVersion");
                snapshot.BaseBoardManufacturer = ReadString(baseKey, "BaseBoardManufacturer");
                snapshot.FirmwareManufacturer = ReadString(baseKey, "FirmwareManufacturer");
                snapshot.OEMModelBaseBoard = ReadString(baseKey, "OEMModelBaseBoard");
                snapshot.FirmwareVersion = ReadString(baseKey, "FirmwareVersion");
                snapshot.OEMModelNumber = ReadString(baseKey, "OEMModelNumber");
                snapshot.OEMModelSystemFamily = ReadString(baseKey, "OEMModelSystemFamily");
                snapshot.OEMName = ReadString(baseKey, "OEMName");
                snapshot.OSArchitecture = ReadString(baseKey, "OSArchitecture");
                snapshot.OEMModelSKU = ReadString(baseKey, "OEMModelSKU");
                snapshot.FirmwareReleaseDate = ReadDateTime(baseKey, "FirmwareReleaseDate");
                snapshot.OEMModelBaseBoardVersion = ReadString(baseKey, "OEMModelBaseBoardVersion");
                snapshot.StateAttributes = ReadString(baseKey, "StateAttributes");
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, "Access denied reading Device Attributes registry keys. Run as Administrator or check permissions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while reading Device Attributes registry keys.");
            }

            return Task.FromResult(snapshot);
        }

        public Task<TelemetryPolicySnapshot> CaptureTelemetryPolicyAsync(CancellationToken cancellationToken)
        {
            var snapshot = new TelemetryPolicySnapshot
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(TelemetryPolicySnapshot.RegistryRootPath, false);
                if (baseKey == null)
                {
                    _logger.LogDebug("Telemetry policy registry path not found at {Path}. Using default telemetry settings.", TelemetryPolicySnapshot.RegistryRootPath);
                    return Task.FromResult(snapshot);
                }

                snapshot.AllowTelemetry = ReadUInt(baseKey, "AllowTelemetry");

                _logger.LogDebug("Telemetry level: {Level} ({Description})", 
                    snapshot.AllowTelemetry, 
                    snapshot.TelemetryLevelDescription);
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, "Access denied reading Telemetry policy registry keys. Run as Administrator or check permissions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while reading Telemetry policy registry keys.");
            }

            return Task.FromResult(snapshot);
        }

        private static uint? ReadUInt(RegistryKey key, string valueName)
        {
            var value = key.GetValue(valueName);
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string? ReadString(RegistryKey key, string valueName)
        {
            var value = key.GetValue(valueName) as string;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static bool? ReadBool(RegistryKey key, string valueName)
        {
            var value = key.GetValue(valueName);
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static DateTimeOffset? ReadDateTimeOffset(RegistryKey key, string valueName)
        {
            var value = key.GetValue(valueName) as byte[];
            if (value == null || value.Length != 8)
            {
                return null;
            }

            try
            {
                var fileTime = BitConverter.ToInt64(value, 0);
                return DateTimeOffset.FromFileTime(fileTime);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static DateTime? ReadDateTime(RegistryKey key, string valueName)
        {
            var value = key.GetValue(valueName) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return DateTime.Parse(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
