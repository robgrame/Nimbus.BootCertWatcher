using SecureBootDashboard.Web.Services;
using Serilog;
using Serilog.Events;
using System.IO;

// Configure Serilog before building the app
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "web-.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("========================================");
    Log.Information("Starting SecureBootDashboard.Web application");
    Log.Information("========================================");
    Log.Information("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
    Log.Information("Base Directory: {BaseDirectory}", AppContext.BaseDirectory);
    Log.Information("Log Path: {LogPath}", Path.GetFullPath(logPath));
    Log.Information("Machine Name: {MachineName}", Environment.MachineName);
    Log.Information("User: {User}", Environment.UserName);
    Log.Information(".NET Version: {DotNetVersion}", Environment.Version);

    var builder = WebApplication.CreateBuilder(args);

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

    // Configure API settings
    builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
    
    // Log API Base URL
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
    if (!string.IsNullOrEmpty(apiBaseUrl))
    {
        Log.Information("API Base URL: {ApiBaseUrl}", apiBaseUrl);
    }
    else
    {
        Log.Warning("API Base URL not configured!");
    }

    // Register HttpClient for API communication
    builder.Services.AddHttpClient<ISecureBootApiClient, SecureBootApiClient>();

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

    app.UseAuthorization();

    app.MapRazorPages();

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
