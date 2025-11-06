using SecureBootDashboard.Web.Services;
using Serilog;
using Serilog.Events;

// Configure Serilog before building the app
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/web-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting SecureBootDashboard.Web application");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddRazorPages();

    // Configure API settings
    builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

    // Register HttpClient for API communication
    builder.Services.AddHttpClient<ISecureBootApiClient, SecureBootApiClient>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthorization();

    app.MapRazorPages();

    Log.Information("SecureBootDashboard.Web started successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SecureBootDashboard.Web terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
