using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SecureBootDashboard.Web.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public LoginModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? ErrorMessage { get; set; }

        public bool IsEntraIdEnabled { get; set; }
        public bool IsWindowsAuthEnabled { get; set; }

        public void OnGet()
        {
            var authProvider = _configuration["Authentication:Provider"];
            IsEntraIdEnabled = string.Equals(authProvider, "EntraId", StringComparison.OrdinalIgnoreCase);
            IsWindowsAuthEnabled = string.Equals(authProvider, "Windows", StringComparison.OrdinalIgnoreCase) ||
                                 _configuration.GetValue<bool>("Authentication:Windows:Enabled");
        }

        public IActionResult OnPostEntraId()
        {
            var redirectUrl = string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl;
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        public IActionResult OnPostWindows()
        {
            var redirectUrl = string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl;
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, NegotiateDefaults.AuthenticationScheme);
        }
    }
}
