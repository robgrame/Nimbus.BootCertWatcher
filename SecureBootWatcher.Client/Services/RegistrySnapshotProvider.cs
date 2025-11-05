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
                using var baseKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\SecureBoot\\Servicing", false);
                if (baseKey == null)
                {
                    _logger.LogWarning("Secure Boot servicing registry path was not found. Returning empty snapshot.");
                    return Task.FromResult(snapshot);
                }

                snapshot.AvailableUpdates = ReadUInt(baseKey, "AvailableUpdates");
                snapshot.UefiCa2023StatusRaw = ReadString(baseKey, "UEFICA2023Status");
                snapshot.UefiCa2023ErrorCode = ReadUInt(baseKey, "UEFICA2023Error");
                snapshot.HighConfidenceOptOut = ReadBool(baseKey, "HighConfidenceOptOut");
                snapshot.MicrosoftUpdateManagedOptIn = ReadBool(baseKey, "MicrosoftUpdateManagedOptIn");
                snapshot.DeploymentState = DeriveDeploymentState(snapshot.UefiCa2023StatusRaw, snapshot.UefiCa2023ErrorCode);
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, "Failed to read Secure Boot registry keys due to insufficient permissions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while reading Secure Boot registry keys.");
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

        private static SecureBootDeploymentState DeriveDeploymentState(string? status, uint? errorCode)
        {
            if (errorCode.HasValue && errorCode.Value != 0)
            {
                return SecureBootDeploymentState.Error;
            }

            return status?.Trim() switch
            {
                "NotStarted" => SecureBootDeploymentState.NotStarted,
                "InProgress" => SecureBootDeploymentState.InProgress,
                "Updated" => SecureBootDeploymentState.Updated,
                _ => SecureBootDeploymentState.Unknown
            };
        }
    }
}
