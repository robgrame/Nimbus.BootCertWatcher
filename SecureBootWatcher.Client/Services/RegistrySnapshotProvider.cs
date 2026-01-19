using System;
using System.Globalization;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;
using System.IO;

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
            _logger.LogDebug("RegistrySnapshotProvider.CaptureAsync: Starting Secure Boot registry snapshot capture");
            
            var snapshot = new SecureBootRegistrySnapshot
            {
                CollectedAtUtc = DateTimeOffset.UtcNow,
                Servicing = new SecureBootServicingRegistrySnapshot(),
                State = new SecureBootStateRegistrySnapshot(),
                Sbat = new SecureBootSbatRegistrySnapshot()
            };

            try
            {
                _logger.LogTrace("RegistrySnapshotProvider.CaptureAsync: Opening registry key at {Path}", SecureBootRegistrySnapshot.RegistryRootPath);
                using var secureBootRegistryRoot = Registry.LocalMachine.OpenSubKey(SecureBootRegistrySnapshot.RegistryRootPath, false);
                if (secureBootRegistryRoot == null)
                {
                    _logger.LogDebug("RegistrySnapshotProvider.CaptureAsync: Secure Boot base registry path not found at {Path}. This is normal for devices without Secure Boot servicing configured.", SecureBootRegistrySnapshot.RegistryRootPath);
                    return Task.FromResult(snapshot);
                }

                _logger.LogTrace("RegistrySnapshotProvider.CaptureAsync: Reading base key values");
                snapshot.AvailableUpdates = ReadUInt(secureBootRegistryRoot, "AvailableUpdates");                
                snapshot.HighConfidenceOptOut = ReadBool(secureBootRegistryRoot, "HighConfidenceOptOut");
                snapshot.MicrosoftUpdateManagedOptIn = ReadBool(secureBootRegistryRoot, "MicrosoftUpdateManagedOptIn");
                
                _logger.LogTrace("RegistrySnapshotProvider.CaptureAsync: Base values - AvailableUpdates={AvailableUpdates}, UpdateType={UpdateType}, MicrosoftUpdateManagedOptIn={OptIn}",
                    snapshot.AvailableUpdates, snapshot.MicrosoftUpdateManagedOptIn);

                //using var servicingKey = secureBootRegistryRoot.OpenSubKey("Servicing", false);
                using var servicingKey = Registry.LocalMachine.OpenSubKey(SecureBootServicingRegistrySnapshot.RegistryRootPath, false);
                if (servicingKey != null)
                {
                    _logger.LogTrace("RegistrySnapshotProvider.CaptureAsync: Reading Servicing subkey");
                    snapshot.Servicing.UefiCa2023Status = ParseDeploymentState(ReadString(servicingKey, "UEFICA2023Status"));
                    snapshot.Servicing.UefiCa2023Error = ReadUInt(servicingKey, "UefiCa2023Error");
                    snapshot.Servicing.RebootRequestedDB = ReadUInt(servicingKey, "RebootRequestedDB");
                    snapshot.Servicing.RebootRequestedDBX = ReadUInt(servicingKey, "RebootRequestedDBX");
                    snapshot.Servicing.RebootRequestedKEK = ReadUInt(servicingKey, "RebootRequestedKEK");
                    snapshot.Servicing.WindowsUEFICA2023Capable = ReadUInt(servicingKey, "WindowsUEFICA2023Capable");
                    snapshot.Servicing.BucketHash = ReadString(servicingKey, "BucketHash");
                    snapshot.Servicing.ConfidenceLevel = ReadString(servicingKey, "ConfidenceLevel");
                    
                    _logger.LogDebug("RegistrySnapshotProvider.CaptureAsync: Servicing values - UefiCa2023Status={Status}, WindowsUEFICA2023Capable={Capable}, BucketHash={Hash}, ConfidenceLevel={Confidence}",
                        snapshot.Servicing.UefiCa2023Status, snapshot.Servicing.WindowsUEFICA2023Capable, snapshot.Servicing.BucketHash, snapshot.Servicing.ConfidenceLevel);
                }
                else
                {
                    _logger.LogDebug("RegistrySnapshotProvider.CaptureAsync: Servicing subkey not found. Device may not have Secure Boot servicing registry keys.");
                }

                using var stateKey = Registry.LocalMachine.OpenSubKey(SecureBootStateRegistrySnapshot.RegistryRootPath, false);
                if (stateKey != null)
                {
                    _logger.LogTrace("RegistrySnapshotProvider.CaptureAsync: Reading State subkey");
                    snapshot.State.PolicyPublisher = ReadString(stateKey, "PolicyPublisher");
                    snapshot.State.PolicyVersion = ReadUInt(stateKey, "PolicyVersion");
                    snapshot.State.UEFISecureBootEnabled = ReadBool(stateKey, "UEFISecureBootEnabled");
                    
                    _logger.LogDebug("RegistrySnapshotProvider.CaptureAsync: State values - UEFISecureBootEnabled={Enabled}, PolicyPublisher={Publisher}",
                        snapshot.State.UEFISecureBootEnabled, snapshot.State.PolicyPublisher);
                }
                else
                {
                    _logger.LogDebug("RegistrySnapshotProvider.CaptureAsync: State subkey not found. UEFI Secure Boot status will be unavailable.");
                }

                using var sbatKey = Registry.LocalMachine.OpenSubKey(SecureBootSbatRegistrySnapshot.RegistryRootPath, false);
                if (sbatKey != null)
                {
                    _logger.LogTrace("RegistrySnapshotProvider.CaptureAsync: Reading SBAT subkey");
                    snapshot.Sbat.SbatLevel = ReadBinary(sbatKey, "SbatLevel");
                    snapshot.Sbat.UpdateStatus = ReadUInt(sbatKey, "UpdateStatus");
                    
                    _logger.LogDebug("RegistrySnapshotProvider.CaptureAsync: SBAT values - SbatLevel={Version}, UpdateStatus={Status}",
                        snapshot.Sbat.SbatLevel, snapshot.Sbat.UpdateStatus);
                }
                else
                {
                    _logger.LogDebug("RegistrySnapshotProvider.CaptureAsync: SBAT subkey not found. SBAT information will be unavailable.");
                }

                _logger.LogInformation("RegistrySnapshotProvider.CaptureAsync: Successfully captured Secure Boot registry snapshot - InferredDeploymentState={State}", 
                    snapshot.State);
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, "RegistrySnapshotProvider.CaptureAsync: Access denied reading Secure Boot registry keys. Run as Administrator or check permissions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RegistrySnapshotProvider.CaptureAsync: Unexpected error while reading Secure Boot registry keys.");
            }

            return Task.FromResult(snapshot);
        }

        public Task<SecureBootDeviceAttributesRegistrySnapshot> CaptureDeviceAttributesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("CaptureDeviceAttributesAsync: Starting Device Attributes registry snapshot capture");
            var snapshot = new SecureBootDeviceAttributesRegistrySnapshot
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                _logger.LogTrace("CaptureDeviceAttributesAsync: Opening registry key at {Path}", SecureBootDeviceAttributesRegistrySnapshot.RegistryRootPath);
                using var baseKey = Registry.LocalMachine.OpenSubKey(SecureBootDeviceAttributesRegistrySnapshot.RegistryRootPath, false);
                if (baseKey == null)
                {
                    _logger.LogDebug("CaptureDeviceAttributesAsync: Device Attributes registry path not found at {Path}. This is normal for devices without Secure Boot servicing configured.", SecureBootDeviceAttributesRegistrySnapshot.RegistryRootPath);
                    return Task.FromResult(snapshot);
                }

                _logger.LogTrace("CaptureDeviceAttributesAsync: Reading device attribute values");
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
                
                _logger.LogDebug("CaptureDeviceAttributesAsync: Successfully captured Device Attributes - Manufacturer={Mfr}, FirmwareVersion={FwVer}, FirmwareReleaseDate={FwDate}",
                    snapshot.OEMManufacturerName, snapshot.FirmwareVersion, snapshot.FirmwareReleaseDate);
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, "CaptureDeviceAttributesAsync: Access denied reading Device Attributes registry keys. Run as Administrator or check permissions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CaptureDeviceAttributesAsync: Unexpected error while reading Device Attributes registry keys.");
            }

            return Task.FromResult(snapshot);
        }

        public Task<TelemetryPolicySnapshot> CaptureTelemetryPolicyAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("CaptureTelemetryPolicyAsync: Starting Telemetry Policy snapshot capture");
            var snapshot = new TelemetryPolicySnapshot
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            };

            try
            {
                _logger.LogTrace("CaptureTelemetryPolicyAsync: Opening registry key at {Path}", TelemetryPolicySnapshot.RegistryRootPath);
                using var baseKey = Registry.LocalMachine.OpenSubKey(TelemetryPolicySnapshot.RegistryRootPath, false);
                if (baseKey == null)
                {
                    _logger.LogDebug("CaptureTelemetryPolicyAsync: Telemetry policy registry path not found at {Path}. Using default telemetry settings.", TelemetryPolicySnapshot.RegistryRootPath);
                    return Task.FromResult(snapshot);
                }

                _logger.LogTrace("CaptureTelemetryPolicyAsync: Reading AllowTelemetry value");
                snapshot.AllowTelemetry = ReadUInt(baseKey, "AllowTelemetry");

                _logger.LogDebug("CaptureTelemetryPolicyAsync: Telemetry level: {Level} ({Description}), MeetsCFR={Meets}", 
                    snapshot.AllowTelemetry, 
                    snapshot.TelemetryLevelDescription,
                    snapshot.MeetsCfrTelemetryRequirement);
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, "CaptureTelemetryPolicyAsync: Access denied reading Telemetry policy registry keys. Run as Administrator or check permissions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CaptureTelemetryPolicyAsync: Unexpected error while reading Telemetry policy registry keys.");
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

        private static byte[]? ReadBinary(RegistryKey key, string valueName)
        {
            var value = key.GetValue(valueName) as byte[];
            return value;
        }

        private static SecureBootDeploymentState? ParseDeploymentState(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value switch
            {
                "NotStarted" => SecureBootDeploymentState.NotStarted,
                "InProgress" => SecureBootDeploymentState.InProgress,
                "Updated" => SecureBootDeploymentState.Updated,
                _ => SecureBootDeploymentState.Unknown
            };
        }
    }
}
