using Microsoft.AspNetCore.Mvc;
using WindowsVersionsCore.Models;
using WindowsVersionsCore.Services;

namespace WindowsVersionsCore.Controllers
{
    /// <summary>
    /// General API controller for Windows version utilities
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class WindowsController : ControllerBase
    {
        private readonly IWindowsService _windowsService;
        private readonly ILogger<WindowsController> _logger;

        public WindowsController(IWindowsService windowsService, ILogger<WindowsController> logger)
        {
            _windowsService = windowsService;
            _logger = logger;
        }

        /// <summary>
        /// Compare two Windows versions
        /// </summary>
        /// <param name="version1">First version to compare</param>
        /// <param name="version2">Second version to compare</param>
        /// <returns>Version comparison result</returns>
        [HttpGet("compare")]
        [ProducesResponseType(typeof(ApiResponse<VersionComparison>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<VersionComparison>>> CompareVersions(
            [FromQuery] string version1, 
            [FromQuery] string version2)
        {
            if (string.IsNullOrWhiteSpace(version1) || string.IsNullOrWhiteSpace(version2))
            {
                return BadRequest(new ApiResponse<VersionComparison>
                {
                    Success = false,
                    Message = "Both version1 and version2 parameters are required",
                    Data = null
                });
            }

            var result = await _windowsService.CompareVersionsAsync(version1, version2);
            return Ok(result);
        }

        /// <summary>
        /// Refresh Windows version data from Microsoft sources
        /// </summary>
        /// <returns>Success status</returns>
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> RefreshData()
        {
            try
            {
                var success = await _windowsService.RefreshDataAsync();
                
                return Ok(new ApiResponse<bool>
                {
                    Success = true,
                    Data = success,
                    Message = success ? "Data refresh completed successfully" : "Data refresh completed with errors",
                    Source = "Manual Refresh"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual data refresh");
                return Ok(new ApiResponse<bool>
                {
                    Success = false,
                    Data = false,
                    Message = $"Data refresh failed: {ex.Message}",
                    Source = "Manual Refresh"
                });
            }
        }

        /// <summary>
        /// Get the last data update timestamp
        /// </summary>
        /// <returns>Last update timestamp</returns>
        [HttpGet("last-update")]
        [ProducesResponseType(typeof(ApiResponse<DateTime?>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<DateTime?>>> GetLastUpdate()
        {
            var lastUpdate = await _windowsService.GetLastUpdateTimeAsync();
            
            return Ok(new ApiResponse<DateTime?>
            {
                Success = true,
                Data = lastUpdate,
                Message = lastUpdate.HasValue ? $"Last updated: {lastUpdate:yyyy-MM-dd HH:mm:ss} UTC" : "No update timestamp available",
                Source = "Storage Metadata"
            });
        }

        /// <summary>
        /// Get application health status
        /// </summary>
        /// <returns>Health status information</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<ActionResult> GetHealth()
        {
            try
            {
                var lastUpdate = await _windowsService.GetLastUpdateTimeAsync();
                var windows10Summary = await _windowsService.GetReleaseSummaryAsync(WindowsEdition.Windows10);
                var windows11Summary = await _windowsService.GetReleaseSummaryAsync(WindowsEdition.Windows11);

                var health = new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    LastDataUpdate = lastUpdate,
                    DataAge = lastUpdate.HasValue ? (TimeSpan?)(DateTime.UtcNow - lastUpdate.Value) : null,
                    Windows10 = new
                    {
                        Available = windows10Summary.Success,
                        TotalVersions = windows10Summary.Data?.RecentVersions?.Count ?? 0,
                        TotalUpdates = windows10Summary.Data?.TotalUpdates ?? 0
                    },
                    Windows11 = new
                    {
                        Available = windows11Summary.Success,
                        TotalVersions = windows11Summary.Data?.RecentVersions?.Count ?? 0,
                        TotalUpdates = windows11Summary.Data?.TotalUpdates ?? 0
                    }
                };

                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking application health");
                return Ok(new
                {
                    Status = "Unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test the Windows version scraper directly without saving to storage
        /// </summary>
        /// <param name="edition">Windows edition to scrape (Windows10 or Windows11)</param>
        /// <returns>List of scraped Windows versions</returns>
        [HttpGet("test-scraper")]
        [ProducesResponseType(typeof(ApiResponse<List<WindowsVersion>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<WindowsVersion>>>> TestScraper(
            [FromQuery] WindowsEdition edition = WindowsEdition.Windows10)
        {
            try
            {
                _logger.LogInformation("Testing scraper for {Edition}", edition);
                // Note: This endpoint is not currently implemented in the service
                return Ok(new ApiResponse<List<WindowsVersion>>
                {
                    Success = false,
                    Data = new List<WindowsVersion>(),
                    Message = "Test scraper endpoint is not yet implemented",
                    Source = "Test"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing scraper for {Edition}", edition);
                return Ok(new ApiResponse<List<WindowsVersion>>
                {
                    Success = false,
                    Data = new List<WindowsVersion>(),
                    Message = $"Scraper test failed: {ex.Message}",
                    Source = "Test"
                });
            }
        }
    }
}