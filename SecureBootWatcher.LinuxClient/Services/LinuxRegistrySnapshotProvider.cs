using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Services
{
    /// <summary>
    /// Linux implementation that reads Secure Boot status from EFI variables
    /// located in /sys/firmware/efi/efivars
    /// </summary>
    internal sealed class LinuxRegistrySnapshotProvider : IRegistrySnapshotProvider
    {
        private readonly ILogger<LinuxRegistrySnapshotProvider> _logger;
        private const string EfiVarsPath = "/sys/firmware/efi/efivars";

        public LinuxRegistrySnapshotProvider(ILogger<LinuxRegistrySnapshotProvider> logger)
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
                // On Linux, we don't have the same registry structure as Windows
                // The Secure Boot deployment tracking is Windows-specific
                // We'll return a minimal snapshot indicating this is Linux
                
                if (!Directory.Exists(EfiVarsPath))
                {
                    _logger.LogWarning("EFI variables path {Path} not found. System may not support UEFI or may need elevated permissions.", EfiVarsPath);
                    snapshot.DeploymentState = SecureBootDeploymentState.Unknown;
                }
                else
                {
                    // Linux doesn't track UEFI CA 2023 deployment like Windows does
                    // This is primarily a Windows feature for tracking Microsoft's certificate updates
                    snapshot.DeploymentState = SecureBootDeploymentState.Unknown;
                    _logger.LogDebug("EFI variables path exists. Secure Boot status will be determined from certificate enumeration.");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Failed to access EFI variables due to insufficient permissions. Try running with sudo.");
                snapshot.DeploymentState = SecureBootDeploymentState.Unknown;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while checking EFI variables.");
                snapshot.DeploymentState = SecureBootDeploymentState.Unknown;
            }

            return Task.FromResult(snapshot);
        }
    }
}
