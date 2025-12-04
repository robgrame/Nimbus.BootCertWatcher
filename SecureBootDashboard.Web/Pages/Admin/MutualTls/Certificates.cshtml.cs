using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SecureBootDashboard.Web.Services;

namespace SecureBootDashboard.Web.Pages.Admin.MutualTls;

public class CertificatesModel : PageModel
{
    private readonly ISecureBootApiClient _apiClient;
    private readonly ILogger<CertificatesModel> _logger;

    public CertificatesModel(ISecureBootApiClient apiClient, ILogger<CertificatesModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public List<TrustedCAViewModel> Certificates { get; set; } = new();

    [BindProperty]
    public IFormFile? CertificateFile { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    public string? StatusMessage { get; set; }
    public bool IsError { get; set; }

    public async Task<IActionResult> OnGetAsync(bool includeDisabled = false)
    {
        try
        {
            var url = $"/api/CertificateAuthorities?includeDisabled={includeDisabled}";
            var certificates = await _apiClient.GetAsync<List<TrustedCADto>>(url);

            if (certificates != null)
            {
                Certificates = certificates.Select(c => new TrustedCAViewModel
                {
                    Id = c.Id,
                    CommonName = c.CommonName,
                    Thumbprint = c.Thumbprint,
                    Thumbprint256 = c.Thumbprint256,
                    Subject = c.Subject,
                    Issuer = c.Issuer,
                    NotBefore = c.NotBefore,
                    NotAfter = c.NotAfter,
                    IsRootCa = c.IsRootCa,
                    SerialNumber = c.SerialNumber,
                    IsEnabled = c.IsEnabled,
                    Description = c.Description,
                    CreatedAtUtc = c.CreatedAtUtc,
                    CreatedBy = c.CreatedBy,
                    UpdatedAtUtc = c.UpdatedAtUtc,
                    UpdatedBy = c.UpdatedBy,
                    IsExpired = c.IsExpired,
                    DaysUntilExpiration = c.DaysUntilExpiration
                }).ToList();
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load trusted CA certificates");
            StatusMessage = $"Failed to load certificates: {ex.Message}";
            IsError = true;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (CertificateFile == null || CertificateFile.Length == 0)
        {
            StatusMessage = "Please select a certificate file to upload";
            IsError = true;
            return RedirectToPage();
        }

        // Validate file extension
        var allowedExtensions = new[] { ".cer", ".crt", ".pem", ".der" };
        var fileExtension = Path.GetExtension(CertificateFile.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(fileExtension))
        {
            StatusMessage = $"Invalid file extension. Allowed: {string.Join(", ", allowedExtensions)}";
            IsError = true;
            return RedirectToPage();
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await CertificateFile.CopyToAsync(memoryStream);
            var certificateData = memoryStream.ToArray();

            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(certificateData), "CertificateFile", CertificateFile.FileName);
            
            if (!string.IsNullOrWhiteSpace(Description))
            {
                content.Add(new StringContent(Description), "Description");
            }

            var response = await _apiClient.PostFormDataAsync<TrustedCADto>("/api/CertificateAuthorities/upload", content);

            StatusMessage = $"Certificate '{response.CommonName}' uploaded successfully";
            IsError = false;

            _logger.LogInformation("Uploaded CA certificate: {CommonName} by {User}", 
                response.CommonName, User.Identity?.Name ?? "Anonymous");

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload CA certificate");
            StatusMessage = $"Failed to upload certificate: {ex.Message}";
            IsError = true;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, bool enabled)
    {
        try
        {
            await _apiClient.PatchAsync($"/api/CertificateAuthorities/{id}/enabled", new { Enabled = enabled });

            StatusMessage = $"Certificate {(enabled ? "enabled" : "disabled")} successfully";
            IsError = false;

            _logger.LogInformation("CA certificate {Id} {Action} by {User}", 
                id, enabled ? "enabled" : "disabled", User.Identity?.Name ?? "Anonymous");

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle CA certificate {Id}", id);
            StatusMessage = $"Failed to toggle certificate: {ex.Message}";
            IsError = true;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            await _apiClient.DeleteAsync($"/api/CertificateAuthorities/{id}");

            StatusMessage = "Certificate deleted successfully";
            IsError = false;

            _logger.LogInformation("CA certificate {Id} deleted by {User}", id, User.Identity?.Name ?? "Anonymous");

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete CA certificate {Id}", id);
            StatusMessage = $"Failed to delete certificate: {ex.Message}";
            IsError = true;
            return RedirectToPage();
        }
    }
}

public class TrustedCAViewModel
{
    public int Id { get; set; }
    public string CommonName { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public string? Thumbprint256 { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset NotAfter { get; set; }
    public bool IsRootCa { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsExpired { get; set; }
    public int DaysUntilExpiration { get; set; }
}

public class TrustedCADto
{
    public int Id { get; set; }
    public string CommonName { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public string? Thumbprint256 { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset NotAfter { get; set; }
    public bool IsRootCa { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsExpired { get; set; }
    public int DaysUntilExpiration { get; set; }
}
