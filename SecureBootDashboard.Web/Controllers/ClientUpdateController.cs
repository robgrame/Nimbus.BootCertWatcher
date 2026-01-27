using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace SecureBootDashboard.Api.Controllers
{
    /// <summary>
    /// Provides client update information and download capabilities.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ClientUpdateController : ControllerBase
    {
        private readonly ILogger<ClientUpdateController> _logger;
        private readonly IConfiguration _configuration;

        public ClientUpdateController(
            ILogger<ClientUpdateController> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Gets the latest available client version.
        /// </summary>
        [HttpGet("version")]
        [ProducesResponseType(typeof(ClientVersionInfo), StatusCodes.Status200OK)]
        public IActionResult GetLatestVersion()
        {
            var versionInfo = new ClientVersionInfo
            {
                LatestVersion = _configuration["ClientUpdate:LatestVersion"] ?? "1.0.0.0",
                ReleaseDate = DateTime.Parse(_configuration["ClientUpdate:ReleaseDate"] ?? DateTime.UtcNow.ToString()),
                DownloadUrl = _configuration["ClientUpdate:DownloadUrl"],
                IsUpdateRequired = bool.Parse(_configuration["ClientUpdate:IsUpdateRequired"] ?? "false"),
                MinimumVersion = _configuration["ClientUpdate:MinimumVersion"] ?? "1.0.0.0",
                ReleaseNotes = _configuration["ClientUpdate:ReleaseNotes"],
                Checksum = _configuration["ClientUpdate:Checksum"], // SHA256 hash for verification
                FileSize = long.Parse(_configuration["ClientUpdate:FileSize"] ?? "0")
            };

            return Ok(versionInfo);
        }

        /// <summary>
        /// Checks if a specific client version needs to be updated.
        /// </summary>
        [HttpGet("check")]
        [ProducesResponseType(typeof(UpdateCheckResult), StatusCodes.Status200OK)]
        public IActionResult CheckForUpdate([FromQuery] string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(currentVersion))
            {
                return BadRequest("Current version is required");
            }

            var latestVersion = _configuration["ClientUpdate:LatestVersion"] ?? "1.0.0.0";
            var minimumVersion = _configuration["ClientUpdate:MinimumVersion"] ?? "1.0.0.0";

            var current = Version.Parse(currentVersion);
            var latest = Version.Parse(latestVersion);
            var minimum = Version.Parse(minimumVersion);

            var result = new UpdateCheckResult
            {
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                UpdateAvailable = current < latest,
                UpdateRequired = current < minimum,
                DownloadUrl = _configuration["ClientUpdate:DownloadUrl"],
                ReleaseNotes = _configuration["ClientUpdate:ReleaseNotes"]
            };

            _logger.LogInformation(
                "Version check: Current={Current}, Latest={Latest}, UpdateAvailable={Available}, Required={Required}",
                currentVersion, latestVersion, result.UpdateAvailable, result.UpdateRequired);

            return Ok(result);
        }

        /// <summary>
        /// Downloads the latest client package (optional - can use Azure Storage instead).
        /// </summary>
        [HttpGet("download")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult DownloadLatestClient()
        {
            var packagePath = _configuration["ClientUpdate:PackagePath"];
            
            if (string.IsNullOrWhiteSpace(packagePath) || !System.IO.File.Exists(packagePath))
            {
                return NotFound("Client package not available for direct download. Use DownloadUrl instead.");
            }

            var fileBytes = System.IO.File.ReadAllBytes(packagePath);
            var fileName = $"SecureBootWatcher-Client-{_configuration["ClientUpdate:LatestVersion"]}.zip";

            return File(fileBytes, "application/zip", fileName);
        }
    }

    public class ClientVersionInfo
    {
        public string LatestVersion { get; set; } = "";
        public DateTime ReleaseDate { get; set; }
        public string? DownloadUrl { get; set; }
        public bool IsUpdateRequired { get; set; }
        public string MinimumVersion { get; set; } = "";
        public string? ReleaseNotes { get; set; }
        public string? Checksum { get; set; }
        public long FileSize { get; set; }
    }

    public class UpdateCheckResult
    {
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public bool UpdateAvailable { get; set; }
        public bool UpdateRequired { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ReleaseNotes { get; set; }
    }
}
