using Microsoft.AspNetCore.Mvc;
using WindowsVersionsCore.Models;
using WindowsVersionsCore.Services;

namespace WindowsVersionsCore.Controllers
{
    /// <summary>
    /// API controller for Windows 10 releases and updates
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class Windows10Controller : ControllerBase
    {
        private readonly IWindowsService _windowsService;
        private readonly ILogger<Windows10Controller> _logger;

        public Windows10Controller(IWindowsService windowsService, ILogger<Windows10Controller> logger)
        {
            _windowsService = windowsService;
            _logger = logger;
        }

        /// <summary>
        /// Get all Windows 10 versions
        /// </summary>
        /// <returns>List of Windows 10 versions</returns>
        [HttpGet("versions")]
        [ProducesResponseType(typeof(ApiResponse<List<WindowsVersion>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<WindowsVersion>>>> GetVersions()
        {
            var result = await _windowsService.GetWindowsVersionsAsync(WindowsEdition.Windows10);
            return Ok(result);
        }

        /// <summary>
        /// Get all Windows 10 updates
        /// </summary>
        /// <returns>List of Windows 10 updates</returns>
        [HttpGet("updates")]
        [ProducesResponseType(typeof(ApiResponse<List<WindowsUpdate>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<WindowsUpdate>>>> GetUpdates()
        {
            var result = await _windowsService.GetWindowsUpdatesAsync(WindowsEdition.Windows10);
            return Ok(result);
        }

        /// <summary>
        /// Get Windows 10 release summary
        /// </summary>
        /// <returns>Summary of Windows 10 releases</returns>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(ApiResponse<WindowsReleaseSummary>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<WindowsReleaseSummary>>> GetSummary()
        {
            var result = await _windowsService.GetReleaseSummaryAsync(WindowsEdition.Windows10);
            return Ok(result);
        }

        /// <summary>
        /// Get the latest Windows 10 version
        /// </summary>
        /// <returns>Latest Windows 10 version</returns>
        [HttpGet("latest")]
        [ProducesResponseType(typeof(ApiResponse<WindowsVersion>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<WindowsVersion?>>> GetLatest()
        {
            var result = await _windowsService.GetLatestVersionAsync(WindowsEdition.Windows10);
            return Ok(result);
        }

        /// <summary>
        /// Get recent Windows 10 updates
        /// </summary>
        /// <param name="count">Number of recent updates to retrieve (default: 10)</param>
        /// <returns>List of recent Windows 10 updates</returns>
        [HttpGet("updates/recent")]
        [ProducesResponseType(typeof(ApiResponse<List<WindowsUpdate>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<WindowsUpdate>>>> GetRecentUpdates([FromQuery] int count = 10)
        {
            var result = await _windowsService.GetRecentUpdatesAsync(WindowsEdition.Windows10, count);
            return Ok(result);
        }

        /// <summary>
        /// Get Windows 10 feature updates
        /// </summary>
        /// <returns>List of Windows 10 feature updates</returns>
        [HttpGet("feature-updates")]
        [ProducesResponseType(typeof(ApiResponse<List<WindowsFeatureUpdate>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<WindowsFeatureUpdate>>>> GetFeatureUpdates()
        {
            var result = await _windowsService.GetFeatureUpdatesAsync(WindowsEdition.Windows10);
            return Ok(result);
        }
    }
}