using Microsoft.Identity.Web;
using SecureBootDashboard.Web.Services;
using Serilog;
using Serilog.Events;
using System.Reflection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization;

// Minimal startup with error resilience
try
{
    // Determine writable log root (App Service: D:\home\LogFiles)
    var home = Environment.GetEnvironmentVariable("HOME");
    var logRoot = !string.IsNullOrEmpty(home)
        ? Path.Combine(home, "LogFiles", "SecureBootDashboard.Web")
        : Path.Combine(AppContext.BaseDirectory, "logs");
    Directory.CreateDirectory(logRoot);

    // Log to file immediately in a writable location
    var startupLogPath = Path.Combine(logRoot, "startup.log");
    using (var writer = File.AppendText(startupLogPath))
    {
        writer.WriteLine($"[{DateTime.UtcNow:O}] App starting - BaseDir: {AppContext.BaseDirectory}");
        writer.WriteLine($"[{DateTime.UtcNow:O}] CWD: {Directory.GetCurrentDirectory()}");
        writer.WriteLine($"[{DateTime.UtcNow:O}] HOME: {home ?? "<null>"}");
        writer.Flush();
    }

    // Configure minimal Serilog first
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console()
        .WriteTo.File(Path.Combine(logRoot, "app-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7)
        .CreateLogger();

    Log.Information("=== SecureBootDashboard.Web Starting ===");
    Log.Information("BaseDirectory: {BaseDir}", AppContext.BaseDirectory);
    Log.Information("Environment: {Env}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog using configuration (appsettings.json / appsettings.Production.json) and DI enrichers
    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();
    });

    // Get version
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
    Log.Information("Version: {Version}", version);

    // Add core services
    builder.Services.AddApplicationInsightsTelemetry();
    builder.Services.AddRazorPages();

    // Configure authentication
    var authProvider = builder.Configuration["Authentication:Provider"];
    var isAppService = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
    Log.Information("Auth Provider: {Provider} (AppService={IsAppService})", authProvider ?? "None", isAppService);

    var useAnonymous = string.IsNullOrEmpty(authProvider) || string.Equals(authProvider, "None", StringComparison.OrdinalIgnoreCase);

    if (!useAnonymous)
    {
        if (string.Equals(authProvider, "EntraId", StringComparison.OrdinalIgnoreCase))
        {
            var clientId = builder.Configuration["Authentication:EntraId:ClientId"];
            var tenantId = builder.Configuration["Authentication:EntraId:TenantId"];

            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(tenantId))
            {
                builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("Authentication:EntraId"));
                Log.Information("Entra ID configured");
            }
            else
            {
                Log.Warning("Entra ID selected but not configured; enabling anonymous access");
                useAnonymous = true;
            }
        }
        else if (string.Equals(authProvider, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            if (isAppService)
            {
                Log.Warning("Windows/Negotiate authentication is not supported on Azure App Service; enabling anonymous access");
                useAnonymous = true;
            }
            else
            {
                builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults.AuthenticationScheme)
                    .AddNegotiate();
                Log.Information("Windows auth configured");
            }
        }
        else
        {
            // Default cookie auth, but only when not anonymous
            builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();
            Log.Information("Cookie auth configured");
        }
    }

    // Authorization is always added; in anonymous mode, allow all
    builder.Services.AddAuthorization(options =>
    {
        if (useAnonymous)
        {
            var allowAll = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();
            options.DefaultPolicy = allowAll;
            options.FallbackPolicy = allowAll;
        }
    });

    // Configure API settings
    builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

    var apiUrl = builder.Configuration["ApiSettings:BaseUrl"];
    if (!string.IsNullOrEmpty(apiUrl))
    {
        Log.Information("API URL: {Url}", apiUrl);
    }

    // Register HTTP clients
    builder.Services.AddHttpClient<ISecureBootApiClient, SecureBootApiClient>()
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var handler = new HttpClientHandler();
            if (builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("ApiSettings:BypassSslValidation"))
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            return handler;
        });

    builder.Services.AddHttpClient("SecureBootApi")
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var handler = new HttpClientHandler();
            if (builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("ApiSettings:BypassSslValidation"))
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            return handler;
        });

    Log.Information("Services configured");

    var app = builder.Build();

    Log.Information("App built");

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // Honor x-forwarded headers from App Service
    var fwdOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
        RequireHeaderSymmetry = false
    };
    fwdOptions.KnownIPNetworks.Clear();
    fwdOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(fwdOptions);

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    if (!useAnonymous)
    {
        app.UseAuthentication();
    }
    // Always add authorization middleware; in anonymous mode policies allow all
    app.UseAuthorization();

    app.MapRazorPages();

    Log.Information("Auth mode: {Mode}", useAnonymous ? "Anonymous" : authProvider ?? "Cookie");
    Log.Information("=== App Started Successfully ===");
    app.Run();
}
catch (Exception ex)
{
    // Write to file even if logging fails
    try
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        var logRoot = !string.IsNullOrEmpty(home)
            ? Path.Combine(home, "LogFiles", "SecureBootDashboard.Web")
            : Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logRoot);
        var logPath = Path.Combine(logRoot, "startup-error.log");
        File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] FATAL: {ex}\n{ex.InnerException}\n\n");
    }
    catch { }

    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
