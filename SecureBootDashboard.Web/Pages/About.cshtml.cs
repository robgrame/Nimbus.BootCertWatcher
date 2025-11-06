using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Reflection;

namespace SecureBootDashboard.Web.Pages;

public class AboutModel : PageModel
{
    public string Version { get; private set; } = "1.0.0";
    public string BuildDate { get; private set; } = string.Empty;

    public void OnGet()
    {
        // Get version from assembly
        var assembly = typeof(AboutModel).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        
        if (informationalVersion != null)
        {
            Version = informationalVersion.InformationalVersion;
        }
        else
        {
            var version = assembly.GetName().Version;
            if (version != null)
            {
                Version = version.ToString();
            }
        }

        // Get build date
        BuildDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
    }
}
