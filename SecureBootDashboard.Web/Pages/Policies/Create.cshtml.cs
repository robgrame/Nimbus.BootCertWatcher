using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootDashboard.Web.Pages.Policies
{
    public class CreateModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(IHttpClientFactory httpClientFactory, ILogger<CreateModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [BindProperty]
        [Required]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public bool IsEnabled { get; set; } = true;

        [BindProperty]
        [Required]
        public int Priority { get; set; } = 100;

        [BindProperty]
        public string? FleetId { get; set; }

        [BindProperty]
        public string? RulesJson { get; set; }

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var rules = new List<PolicyRule>();
                if (!string.IsNullOrEmpty(RulesJson))
                {
                    try
                    {
                        rules = JsonSerializer.Deserialize<List<PolicyRule>>(RulesJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? new List<PolicyRule>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize rules JSON");
                        ErrorMessage = "Invalid rules data. Please check your rule configuration.";
                        return Page();
                    }
                }

                var policy = new CertificateCompliancePolicy
                {
                    Name = Name,
                    Description = Description,
                    IsEnabled = IsEnabled,
                    Priority = Priority,
                    FleetId = FleetId,
                    Rules = rules
                };

                var client = _httpClientFactory.CreateClient("SecureBootApi");
                var json = JsonSerializer.Serialize(policy);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/api/Policies", content);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage("./List");
                }
                else
                {
                    ErrorMessage = $"Failed to create policy: {response.StatusCode}";
                    _logger.LogWarning("Failed to create policy. Status: {StatusCode}", response.StatusCode);
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while creating the policy.";
                _logger.LogError(ex, "Error creating policy");
                return Page();
            }
        }
    }
}
