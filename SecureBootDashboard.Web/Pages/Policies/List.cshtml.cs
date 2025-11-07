using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Web.Pages.Policies
{
    public class ListModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ListModel> _logger;

        public ListModel(IHttpClientFactory httpClientFactory, ILogger<ListModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public List<CertificateCompliancePolicy> Policies { get; set; } = new List<CertificateCompliancePolicy>();
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("SecureBootApi");
                var response = await client.GetAsync("/api/Policies");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Policies = JsonSerializer.Deserialize<List<CertificateCompliancePolicy>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<CertificateCompliancePolicy>();
                }
                else
                {
                    ErrorMessage = $"Failed to load policies: {response.StatusCode}";
                    _logger.LogWarning("Failed to load policies. Status: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while loading policies.";
                _logger.LogError(ex, "Error loading policies");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("SecureBootApi");
                var response = await client.DeleteAsync($"/api/Policies/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    ErrorMessage = $"Failed to delete policy: {response.StatusCode}";
                    _logger.LogWarning("Failed to delete policy {PolicyId}. Status: {StatusCode}", id, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while deleting the policy.";
                _logger.LogError(ex, "Error deleting policy {PolicyId}", id);
            }

            return RedirectToPage();
        }
    }
}
