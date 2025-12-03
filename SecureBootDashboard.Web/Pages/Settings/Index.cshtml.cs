using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Models;
using SecureBootDashboard.Web.Services;
using System.Text.Json;

namespace SecureBootDashboard.Web.Pages.Settings;

public class IndexModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ISecureBootApiClient apiClient, ILogger<IndexModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public List<ApplicationSettingViewModel> Settings { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? SelectedCategory { get; set; }
    public string? SearchQuery { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public string? CategoryFilter { get; set; }

    public async Task<IActionResult> OnGetAsync(string? category = null, string? search = null)
    {
        try
        {
            SelectedCategory = category;
            SearchQuery = search;

            // Load all settings from API
            var response = await _apiClient.GetAsync<List<ApplicationSettingViewModel>>(
                "/api/Settings",
                HttpContext.RequestAborted);

            if (response == null)
            {
                ErrorMessage = "Failed to load settings from API";
                return Page();
            }

            Settings = response;

            // Extract unique categories
            Categories = Settings
                .Select(s => s.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Apply filters
            if (!string.IsNullOrEmpty(category))
            {
                Settings = Settings.Where(s => s.Category == category).ToList();
            }

            if (!string.IsNullOrEmpty(search))
            {
                Settings = Settings.Where(s =>
                    s.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (s.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
            ErrorMessage = $"Error loading settings: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRefreshCacheAsync()
    {
        try
        {
            var response = await _apiClient.PostAsync<object>(
                "/api/Settings/cache/refresh",
                null,
                HttpContext.RequestAborted);

            SuccessMessage = "Settings cache refreshed successfully";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache");
            ErrorMessage = $"Error refreshing cache: {ex.Message}";
            return RedirectToPage();
        }
    }
}
