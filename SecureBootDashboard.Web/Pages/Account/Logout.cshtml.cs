using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SecureBootDashboard.Web.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public LogoutModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IActionResult> OnGet()
        {
            var authProvider = _configuration["Authentication:Provider"];

            if (string.Equals(authProvider, "EntraId", StringComparison.OrdinalIgnoreCase))
            {
                // Sign out from both cookie authentication and OpenID Connect
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return SignOut(
                    new AuthenticationProperties { RedirectUri = "/Welcome" },
                    OpenIdConnectDefaults.AuthenticationScheme
                );
            }
            else
            {
                // For Windows authentication, just clear the cookie
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToPage("/Welcome");
            }
        }
    }
}
