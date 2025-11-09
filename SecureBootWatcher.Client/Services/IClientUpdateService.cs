using System.Threading;
using System.Threading.Tasks;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Service for checking and applying client updates.
    /// </summary>
    public interface IClientUpdateService
    {
        /// <summary>
        /// Checks if a newer version of the client is available.
        /// </summary>
        Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads and prepares an update for installation.
        /// </summary>
        Task<UpdateDownloadResult> DownloadUpdateAsync(string downloadUrl, CancellationToken cancellationToken = default);

        /// <summary>
        /// Schedules the update to be applied after the current execution completes.
        /// </summary>
        Task<bool> ScheduleUpdateAsync(string updatePackagePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current version of the client.
        /// </summary>
        string GetCurrentVersion();
    }

    public class UpdateCheckResult
    {
        public bool UpdateAvailable { get; set; }
        public bool UpdateRequired { get; set; }
        public string? CurrentVersion { get; set; }
        public string? LatestVersion { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ReleaseNotes { get; set; }
        public string? Checksum { get; set; }
        public long FileSize { get; set; }
    }

    public class UpdateDownloadResult
    {
        public bool Success { get; set; }
        public string? LocalPath { get; set; }
        public string? ErrorMessage { get; set; }
        public bool ChecksumVerified { get; set; }
    }
}
