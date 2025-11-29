using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WindowsVersionsCore.Services;

namespace WindowsVersionsCore.Pages
{
    public class CompareModel : PageModel
    {
        private readonly IWindowsService _windowsService;
        private readonly ILogger<CompareModel> _logger;

        public CompareModel(IWindowsService windowsService, ILogger<CompareModel> logger)
        {
            _windowsService = windowsService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Compare page");
                return RedirectToPage("/Error");
            }
        }
    }
}