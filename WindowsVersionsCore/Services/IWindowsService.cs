using WindowsVersionsCore.Models;

namespace WindowsVersionsCore.Services
{
    /// <summary>
    /// Interface for Windows version tracking service
    /// </summary>
    public interface IWindowsService
    {
        Task<ApiResponse<List<WindowsVersion>>> GetWindowsVersionsAsync(WindowsEdition edition);
        Task<ApiResponse<List<WindowsUpdate>>> GetWindowsUpdatesAsync(WindowsEdition edition);
        Task<ApiResponse<WindowsReleaseSummary>> GetReleaseSummaryAsync(WindowsEdition edition);
        Task<ApiResponse<WindowsVersion?>> GetLatestVersionAsync(WindowsEdition edition);
        Task<ApiResponse<List<WindowsUpdate>>> GetRecentUpdatesAsync(WindowsEdition edition, int count = 10);
        Task<ApiResponse<List<WindowsFeatureUpdate>>> GetFeatureUpdatesAsync(WindowsEdition edition);
        Task<ApiResponse<VersionComparison>> CompareVersionsAsync(string version1, string version2);
        Task<bool> RefreshDataAsync();
        Task<DateTime?> GetLastUpdateTimeAsync();
    }
}