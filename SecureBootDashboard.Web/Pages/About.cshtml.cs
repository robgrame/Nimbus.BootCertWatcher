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
            var fullVersion = informationalVersion.InformationalVersion;
            
            // Remove commit hash (everything after '+') if present
            // Example: "1.1.1.48182+a1b2c3d" -> "1.1.1.48182"
            var plusIndex = fullVersion.IndexOf('+');
            Version = plusIndex > 0 
                ? fullVersion.Substring(0, plusIndex) 
                : fullVersion;
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
