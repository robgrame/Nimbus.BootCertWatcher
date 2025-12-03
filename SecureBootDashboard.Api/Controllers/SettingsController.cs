using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;

namespace SecureBootDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SettingsController : ControllerBase
{
    private readonly SecureBootDbContext _dbContext;
    private readonly IApplicationSettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        SecureBootDbContext dbContext,
        IApplicationSettingsService settingsService,
        ILogger<SettingsController> logger)
    {
        _dbContext = dbContext;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Get all settings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);

        return Ok(settings);
    }

    /// <summary>
    /// Get settings by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<IActionResult> GetCategorySettingsAsync(string category, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(s => s.Category == category)
            .OrderBy(s => s.Key)
            .ToListAsync(cancellationToken);

        if (!settings.Any())
        {
            return NotFound(new { Error = $"No settings found for category '{category}'" });
        }

        return Ok(settings);
    }

    /// <summary>
    /// Get setting by key
    /// </summary>
    [HttpGet("key/{key}")]
    public async Task<IActionResult> GetSettingByKeyAsync(string key, CancellationToken cancellationToken)
    {
        var setting = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting == null)
        {
            return NotFound(new { Error = $"Setting with key '{key}' not found" });
        }

        return Ok(setting);
    }

    /// <summary>
    /// Update setting value
    /// </summary>
    [HttpPut("key/{key}")]
    public async Task<IActionResult> UpdateSettingAsync(
        string key,
        [FromBody] UpdateSettingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var setting = await _dbContext.ApplicationSettings
                .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

            if (setting == null)
            {
                return NotFound(new { Error = $"Setting with key '{key}' not found" });
            }

            // Update value
            setting.Value = request.Value;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
            setting.UpdatedBy = request.UpdatedBy ?? User.Identity?.Name ?? "API";

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Refresh cache
            await _settingsService.RefreshCacheAsync(cancellationToken);

            _logger.LogInformation(
                "Setting {Key} updated to '{Value}' by {UpdatedBy}",
                key,
                setting.IsSensitive ? "***" : request.Value,
                setting.UpdatedBy);

            return Ok(setting);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update setting {Key}", key);
            return StatusCode(500, new { Error = "Failed to update setting" });
        }
    }

    /// <summary>
    /// Refresh settings cache
    /// </summary>
    [HttpPost("cache/refresh")]
    public async Task<IActionResult> RefreshCacheAsync(CancellationToken cancellationToken)
    {
        await _settingsService.RefreshCacheAsync(cancellationToken);
        _logger.LogInformation("Settings cache refreshed via API");
        return Ok(new { Message = "Cache refreshed successfully" });
    }

    /// <summary>
    /// Get settings that require restart
    /// </summary>
    [HttpGet("restart-required")]
    public async Task<IActionResult> GetRestartRequiredSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(s => s.RequiresRestart)
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);

        return Ok(settings);
    }
}

/// <summary>
/// Request to update a setting value
/// </summary>
public sealed record UpdateSettingRequest(
    string Value,
    string? UpdatedBy = null);
