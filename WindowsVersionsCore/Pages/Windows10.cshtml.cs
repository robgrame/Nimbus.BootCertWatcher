using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WindowsVersionsCore.Models;
using WindowsVersionsCore.Services;

namespace WindowsVersionsCore.Pages
{
    public class Windows10Model : PageModel
    {
        private readonly IWindowsService _windowsService;
        private readonly ILogger<Windows10Model> _logger;

        public Windows10Model(IWindowsService windowsService, ILogger<Windows10Model> logger)
        {
            _windowsService = windowsService;
            _logger = logger;
        }

        public WindowsReleaseSummary? Summary { get; private set; }
        public DateTime? LastUpdateDate { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var summaryResponse = await _windowsService.GetReleaseSummaryAsync(WindowsEdition.Windows10);
                if (summaryResponse.Success)
                {
                    Summary = summaryResponse.Data;
                }
                else
                {
                    _logger.LogWarning("Failed to retrieve Windows 10 summary: {Message}", summaryResponse.Message);
                }

                LastUpdateDate = await _windowsService.GetLastUpdateTimeAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Windows 10 page");
                return RedirectToPage("/Error");
            }
        }
    }
}