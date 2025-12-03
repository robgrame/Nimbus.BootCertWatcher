using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Models;
using SecureBootDashboard.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SecureBootDashboard.Web.Pages.Settings;

public class EditModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<EditModel> _logger;

    public EditModel(ISecureBootApiClient apiClient, ILogger<EditModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [BindProperty]
    public EditSettingViewModel Input { get; set; } = new();

    public ApplicationSettingViewModel? Setting { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            // Load setting from API
            var settings = await _apiClient.GetAsync<List<ApplicationSettingViewModel>>(
                "/api/Settings",
                HttpContext.RequestAborted);

            Setting = settings?.FirstOrDefault(s => s.Id == id);

            if (Setting == null)
            {
                ErrorMessage = $"Setting with ID {id} not found";
                return Page();
            }

            // Populate input model
            Input = new EditSettingViewModel
            {
                Id = Setting.Id,
                Key = Setting.Key,
                Value = Setting.Value,
                Category = Setting.Category,
                ValueType = Setting.ValueType,
                Description = Setting.Description,
                IsSensitive = Setting.IsSensitive,
                RequiresRestart = Setting.RequiresRestart
            };

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading setting {SettingId}", id);
            ErrorMessage = $"Error loading setting: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            // Reload setting for display
            var settings = await _apiClient.GetAsync<List<ApplicationSettingViewModel>>(
                "/api/Settings",
                HttpContext.RequestAborted);
            Setting = settings?.FirstOrDefault(s => s.Id == Input.Id);
            return Page();
        }

        try
        {
            // Validate value based on type
            if (!ValidateValue(Input.Value, Input.ValueType, out var validationError))
            {
                ModelState.AddModelError("Input.Value", validationError);
                
                // Reload setting for display
                var settings = await _apiClient.GetAsync<List<ApplicationSettingViewModel>>(
                    "/api/Settings",
                    HttpContext.RequestAborted);
                Setting = settings?.FirstOrDefault(s => s.Id == Input.Id);
                return Page();
            }

            // Update setting via API
            var updateRequest = new UpdateSettingRequest
            {
                Value = Input.Value,
                UpdatedBy = User.Identity?.Name ?? "Web UI"
            };

            var response = await _apiClient.PutAsync<ApplicationSettingViewModel>(
                $"/api/Settings/key/{Uri.EscapeDataString(Input.Key)}",
                updateRequest,
                HttpContext.RequestAborted);

            if (response == null)
            {
                ErrorMessage = "Failed to update setting";
                
                // Reload setting for display
                var settings = await _apiClient.GetAsync<List<ApplicationSettingViewModel>>(
                    "/api/Settings",
                    HttpContext.RequestAborted);
                Setting = settings?.FirstOrDefault(s => s.Id == Input.Id);
                return Page();
            }

            // Redirect back to list with success message
            TempData["SuccessMessage"] = $"Setting '{Input.Key}' updated successfully";
            
            if (Input.RequiresRestart)
            {
                TempData["WarningMessage"] = "?? Application restart required for this change to take effect";
            }

            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating setting {SettingKey}", Input.Key);
            ErrorMessage = $"Error updating setting: {ex.Message}";
            
            // Reload setting for display
            var settings = await _apiClient.GetAsync<List<ApplicationSettingViewModel>>(
                "/api/Settings",
                HttpContext.RequestAborted);
            Setting = settings?.FirstOrDefault(s => s.Id == Input.Id);
            return Page();
        }
    }

    private bool ValidateValue(string value, string valueType, out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            switch (valueType.ToLowerInvariant())
            {
                case "int":
                    if (!int.TryParse(value, out _))
                    {
                        errorMessage = "Value must be a valid integer";
                        return false;
                    }
                    break;

                case "bool":
                    if (!bool.TryParse(value, out _))
                    {
                        errorMessage = "Value must be 'true' or 'false'";
                        return false;
                    }
                    break;

                case "timespan":
                    if (!TimeSpan.TryParse(value, out _))
                    {
                        errorMessage = "Value must be a valid TimeSpan (e.g., '00:00:30')";
                        return false;
                    }
                    break;

                case "datetime":
                case "datetimeoffset":
                    if (!DateTimeOffset.TryParse(value, out _))
                    {
                        errorMessage = "Value must be a valid date/time";
                        return false;
                    }
                    break;

                case "string":
                    // Validate it's valid JSON string (if starts with quote)
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        try
                        {
                            System.Text.Json.JsonSerializer.Deserialize<string>(value);
                        }
                        catch
                        {
                            errorMessage = "Value must be a valid JSON string (e.g., \"text\")";
                            return false;
                        }
                    }
                    break;

                case "json":
                    try
                    {
                        System.Text.Json.JsonDocument.Parse(value);
                    }
                    catch
                    {
                        errorMessage = "Value must be valid JSON";
                        return false;
                    }
                    break;
            }

            return true;
        }
        catch
        {
            errorMessage = $"Invalid value for type '{valueType}'";
            return false;
        }
    }
}

public sealed class EditSettingViewModel
{
    public int Id { get; set; }
    
    [Required]
    public string Key { get; set; } = string.Empty;
    
    [Required]
    public string Value { get; set; } = string.Empty;
    
    [Required]
    public string Category { get; set; } = string.Empty;
    
    [Required]
    public string ValueType { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    public bool IsSensitive { get; set; }
    public bool RequiresRestart { get; set; }
}
