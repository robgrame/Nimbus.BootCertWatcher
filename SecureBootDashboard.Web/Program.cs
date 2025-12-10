using Microsoft.Identity.Web;
using SecureBootDashboard.Web.Services;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

// Configure Serilog before building the app

// Build a temporary configuration to read environment and appsettings
var tempConfig = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Read Application Insights connection string from environment or config
var appInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
    ?? tempConfig["ApplicationInsights:ConnectionString"];

// Read logging level from configuration
var defaultLogLevel = tempConfig["Logging:LogLevel:Default"] ?? "Information";

// DEBUG: Print to console what we're reading
Console.WriteLine("=== LOGGING CONFIGURATION DEBUG ===");
Console.WriteLine($"ASPNETCORE_ENVIRONMENT: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "NOT SET"}");
Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
Console.WriteLine($"Config Default Log Level: {defaultLogLevel}");
Console.WriteLine($"Config Microsoft.AspNetCore: {tempConfig["Logging:LogLevel:Microsoft.AspNetCore"] ?? "NOT SET"}");

// Read Serilog file path from configuration
var serilogFilePath = tempConfig["Serilog:WriteTo:1:Args:path"] ?? "logs/web-.log";
Console.WriteLine($"Serilog File Path (from config): {serilogFilePath}");
Console.WriteLine("====================================");

var logLevel = defaultLogLevel switch
{
    "Trace" => LogEventLevel.Verbose,
    "Debug" => LogEventLevel.Debug,
    "Information" => LogEventLevel.Information,
    "Warning" => LogEventLevel.Warning,
    "Error" => LogEventLevel.Error,
    "Critical" => LogEventLevel.Fatal,
    _ => LogEventLevel.Information
};

Console.WriteLine($"Mapped to Serilog Level: {logLevel}");
Console.WriteLine("====================================");

// Configure Serilog using configuration from appsettings.json
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(tempConfig)
    .MinimumLevel.Is(logLevel)
    .Enrich.FromLogContext();


// Add Application Insights sink if connection string is available
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    loggerConfig.WriteTo.ApplicationInsights(
        connectionString: appInsightsConnectionString,
        telemetryConverter: new TraceTelemetryConverter(),
        restrictedToMinimumLevel: LogEventLevel.Information);
}

Log.Logger = loggerConfig.CreateLogger();

try
{
    // Get version info
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                  ?? assembly.GetName().Version?.ToString()
                  ?? "Unknown";

    Log.Information("========================================");
    Log.Information("Starting SecureBootDashboard.Web application");
    Log.Information("========================================");
    Log.Information("Version: {Version}", version);
    Log.Information("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
    Log.Information("Logging Level: {LogLevel}", defaultLogLevel);
    Log.Information("Base Directory: {BaseDirectory}", AppContext.BaseDirectory);
    Log.Information("Log File Path: {LogPath}", serilogFilePath);
    Log.Information("Machine Name: {MachineName}", Environment.MachineName);
    Log.Information("User: {User}", Environment.UserName);
    Log.Information(".NET Version: {DotNetVersion}", Environment.Version);

    if (!string.IsNullOrEmpty(appInsightsConnectionString))
    {
        Log.Information("Application Insights: Enabled (Connection string configured)");
    }
    else
    {
        Log.Information("Application Insights: Disabled (No connection string found)");
    }

    var builder = WebApplication.CreateBuilder(args);

    // Add Application Insights telemetry
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        // Connection string can come from environment variable or appsettings.json
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            options.ConnectionString = appInsightsConnectionString;
        }

        // Enable adaptive sampling to control telemetry volume
        options.EnableAdaptiveSampling = true;

        // Collect detailed dependency telemetry
        options.EnableDependencyTrackingTelemetryModule = true;

        // Collect performance counters
        options.EnablePerformanceCounterCollectionModule = true;

        // Track unhandled exceptions
        options.EnableQuickPulseMetricStream = true;
    });

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Log configuration sources
    Log.Information("Configuration Sources:");
    foreach (var source in builder.Configuration.Sources)
    {
        Log.Information("  - {Source}", source.ToString());
    }

    // Add services to the container.
    builder.Services.AddRazorPages();

    // Configure authentication based on provider
    var authProvider = builder.Configuration["Authentication:Provider"];
    Log.Information("Authentication Provider: {AuthProvider}", authProvider ?? "None");

    if (string.Equals(authProvider, "EntraId", StringComparison.OrdinalIgnoreCase))
    {
        var clientId = builder.Configuration["Authentication:EntraId:ClientId"];
        var tenantId = builder.Configuration["Authentication:EntraId:TenantId"];

        if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(tenantId))
        {
            // Configure Entra ID (Azure AD) authentication
            builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("Authentication:EntraId"));

            Log.Information("Entra ID authentication configured with ClientId: {ClientId}", clientId);
        }
        else
        {
            Log.Warning("Entra ID authentication selected but ClientId or TenantId not configured. Authentication disabled.");
            // Add cookie authentication as fallback
            builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();
        }
    }
    else if (string.Equals(authProvider, "Windows", StringComparison.OrdinalIgnoreCase))
    {
        // Configure Windows authentication
        builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults.AuthenticationScheme)
            .AddNegotiate();

        Log.Information("Windows authentication configured");
    }
    else
    {
        // No authentication configured - add cookie authentication for session management
        builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();

        Log.Information("No authentication provider configured - using cookie authentication only");
    }

    builder.Services.AddAuthorization(options =>
    {
        // Configure default authorization policy based on authentication provider
        if (string.IsNullOrEmpty(authProvider) || string.Equals(authProvider, "None", StringComparison.OrdinalIgnoreCase))
        {
            // When authentication is disabled, allow anonymous access
            options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAssertion(_ => true)
                .Build();
        }
    });

    // Configure API settings
    builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

    // Log API Base URL
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
    var useCertAuth = builder.Configuration.GetValue<bool>("ApiSettings:UseCertificateAuth");
    if (!string.IsNullOrEmpty(apiBaseUrl))
    {
        Log.Information("API Base URL: {ApiBaseUrl}", apiBaseUrl);
        Log.Information("API Certificate Authentication: {Enabled}", useCertAuth ? "Enabled" : "Disabled");
    }
    else
    {
        Log.Warning("API Base URL not configured!");
    }

    // Register HttpClient for API communication with certificate support
    builder.Services.AddHttpClient<ISecureBootApiClient, SecureBootApiClient>()
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var apiSettings = builder.Configuration.GetSection("ApiSettings").Get<ApiSettings>();
            var handler = new HttpClientHandler();

            // In development, ignore SSL certificate validation errors
            if (builder.Environment.IsDevelopment())
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                Log.Information("Development mode: SSL certificate validation disabled for API client");
            }

            // Configure certificate authentication if enabled
            if (apiSettings?.UseCertificateAuth == true)
            {
                System.Security.Cryptography.X509Certificates.X509Certificate2? certificate = null;

                try
                {
                    // Try to load from certificate store first
                    if (!string.IsNullOrEmpty(apiSettings.CertificateThumbprint))
                    {
                        Log.Information("Loading API client certificate from store: {Thumbprint}", apiSettings.CertificateThumbprint);

                        var storeLocation = apiSettings.CertificateStoreLocation.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase)
                            ? System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine
                            : System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser;

                        var storeName = apiSettings.CertificateStoreName.Equals("Root", StringComparison.OrdinalIgnoreCase)
                            ? System.Security.Cryptography.X509Certificates.StoreName.Root
                            : System.Security.Cryptography.X509Certificates.StoreName.My;

                        // Suppress SYSLIB0057: X509Store.Certificates uses obsolete constructors internally
#pragma warning disable SYSLIB0057
                        using (var store = new System.Security.Cryptography.X509Certificates.X509Store(storeName, storeLocation))
                        {
                            store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
                            var certificates = store.Certificates.Find(
                                System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,
                                apiSettings.CertificateThumbprint,
                                false);

                            if (certificates.Count > 0)
                            {
                                certificate = certificates[0];
                                Log.Information("API client certificate loaded from store: Subject={Subject}, Issuer={Issuer}",
                                    certificate.Subject, certificate.Issuer);
                            }
                            else
                            {
                                Log.Error("API client certificate not found in store with thumbprint: {Thumbprint}",
                                    apiSettings.CertificateThumbprint);
                            }
                        }
#pragma warning restore SYSLIB0057
                    }
                    // Otherwise try to load from file
                    else if (!string.IsNullOrEmpty(apiSettings.CertificatePath))
                    {
                        Log.Information("Loading API client certificate from file: {Path}", apiSettings.CertificatePath);

                        if (!string.IsNullOrEmpty(apiSettings.CertificatePassword))
                        {
                            certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                                apiSettings.CertificatePath,
                                apiSettings.CertificatePassword,
                                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.DefaultKeySet);
                        }
                        else
                        {
                            certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(
                                apiSettings.CertificatePath);
                        }

                        Log.Information("API client certificate loaded from file: Subject={Subject}, Issuer={Issuer}",
                            certificate.Subject, certificate.Issuer);
                    }

                    if (certificate != null)
                    {
                        handler.ClientCertificates.Add(certificate);
                        Log.Information("API client certificate added to HttpClient handler");
                    }
                    else
                    {
                        Log.Warning("Certificate authentication enabled but no certificate could be loaded");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load API client certificate for mutual TLS");
                }
            }

            return handler;
        });

    // Register named HttpClient for backward compatibility (used by Command pages)
    builder.Services.AddHttpClient("SecureBootApi")
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var apiSettings = builder.Configuration.GetSection("ApiSettings").Get<ApiSettings>();
            var handler = new HttpClientHandler();

            // In development, ignore SSL certificate validation errors
            if (builder.Environment.IsDevelopment())
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                Log.Information("Development mode: SSL certificate validation disabled for named HttpClient 'SecureBootApi'");
            }

            // Configure certificate authentication if enabled
            if (apiSettings?.UseCertificateAuth == true)
            {
                System.Security.Cryptography.X509Certificates.X509Certificate2? certificate = null;

                try
                {
                    // Try to load from certificate store first
                    if (!string.IsNullOrEmpty(apiSettings.CertificateThumbprint))
                    {
                        var storeLocation = apiSettings.CertificateStoreLocation.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase)
                            ? System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine
                            : System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser;

                        var storeName = apiSettings.CertificateStoreName.Equals("Root", StringComparison.OrdinalIgnoreCase)
                            ? System.Security.Cryptography.X509Certificates.StoreName.Root
                            : System.Security.Cryptography.X509Certificates.StoreName.My;

#pragma warning disable SYSLIB0057
                        using (var store = new System.Security.Cryptography.X509Certificates.X509Store(storeName, storeLocation))
                        {
                            store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
                            var certificates = store.Certificates.Find(
                                System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,
                                apiSettings.CertificateThumbprint,
                                false);

                            if (certificates.Count > 0)
                            {
                                certificate = certificates[0];
                            }
                        }
#pragma warning restore SYSLIB0057
                    }
                    else if (!string.IsNullOrEmpty(apiSettings.CertificatePath))
                    {
                        if (!string.IsNullOrEmpty(apiSettings.CertificatePassword))
                        {
                            certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                                apiSettings.CertificatePath,
                                apiSettings.CertificatePassword,
                                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.DefaultKeySet);
                        }
                        else
                        {
                            certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(
                                apiSettings.CertificatePath);
                        }
                    }

                    if (certificate != null)
                    {
                        handler.ClientCertificates.Add(certificate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load API client certificate for named HttpClient 'SecureBootApi'");
                }
            }

            return handler;
        });

    Log.Information("HttpClient 'SecureBootApi' registered for command management pages");

    var app = builder.Build();

    // Log URLs configuration
    var urls = builder.Configuration["Urls"];
    if (!string.IsNullOrEmpty(urls))
    {
        Log.Information("Configured URLs: {Urls}", urls);
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
        Log.Information("HSTS enabled");
    }
    else
    {
        Log.Information("Development mode - HSTS disabled");
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapRazorPages();

    // Redirect root to appropriate page based on authentication configuration
    app.MapGet("/", (Microsoft.AspNetCore.Http.HttpContext context) =>
    {
        var authProvider = app.Configuration["Authentication:Provider"];
        if (string.IsNullOrEmpty(authProvider) || string.Equals(authProvider, "None", StringComparison.OrdinalIgnoreCase))
        {
            // No authentication - go directly to Index
            return Results.Redirect("/Index");
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            // Already authenticated - go to Index
            return Results.Redirect("/Index");
        }
        else
        {
            // Need authentication - go to Welcome
            return Results.Redirect("/Welcome");
        }
    });

    Log.Information("========================================");
    Log.Information("SecureBootDashboard.Web started successfully");
    Log.Information("========================================");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SecureBootDashboard.Web terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("Application shutting down...");
    Log.CloseAndFlush();
}

