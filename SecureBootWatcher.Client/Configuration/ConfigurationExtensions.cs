using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecureBootWatcher.Shared.Configuration;

namespace SecureBootWatcher.Client.Configuration
{
    internal static class ConfigurationExtensions
    {
        public static IServiceCollection AddSecureBootWatcherOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SecureBootWatcherOptions>(configuration.GetSection("SecureBootWatcher"));
            return services;
        }
    }
}
