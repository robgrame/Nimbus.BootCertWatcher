using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Hubs;
using SecureBootDashboard.Api.Services;
using SecureBootDashboard.Api.Storage;
using SecureBootWatcher.Shared.Storage;
using Serilog;
using Serilog.Events;

// Configure Serilog before building the app
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "api-.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
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
    Log.Information("Starting SecureBootDashboard.Api application");
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

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Add CORS for SignalR (allow Web frontend to connect)
    Log.Information("Configuring CORS...");
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWebApp", policy =>
        {
            // Get web app URL from configuration or use default
            var webAppUrl = builder.Configuration.GetValue<string>("WebAppUrl") ?? "https://localhost:7001";
            var alternativeUrls = builder.Configuration.GetSection("AlternativeWebUrls").Get<string[]>() ?? Array.Empty<string>();
            
            var allowedOrigins = new List<string> { webAppUrl };
            allowedOrigins.AddRange(alternativeUrls);
            
            policy.WithOrigins(allowedOrigins.ToArray())
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials(); // Required for SignalR
            
            Log.Information("CORS configured for origins: {Origins}", string.Join(", ", allowedOrigins));
        });
    });

    // Add SignalR for real-time updates
    Log.Information("Configuring SignalR...");
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    });
    Log.Information("SignalR configured successfully");

    // Log connection string (masked)
    var connectionString = builder.Configuration.GetConnectionString("SqlServer");
    if (!string.IsNullOrEmpty(connectionString))
    {
        var maskedConnectionString = MaskConnectionString(connectionString);
        Log.Information("SQL Server Connection: {ConnectionString}", maskedConnectionString);
    }
    else
    {
        Log.Warning("No SQL Server connection string found in configuration!");
    }

    Log.Information("Configuring DbContext...");
    builder.Services.AddDbContext<SecureBootDbContext>(options =>
    {
        options.UseSqlServer(connectionString);
        // Disable sensitive data logging in production
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
    });

    builder.Services.AddHealthChecks();

    Log.Information("Configuring Storage services...");
    builder.Services.Configure<FileReportStoreOptions>(builder.Configuration.GetSection("Storage:File"));
    builder.Services.AddScoped<EfCoreReportStore>();
    builder.Services.AddScoped<FileReportStore>();
    builder.Services.AddScoped<IReportStore>(sp =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var provider = configuration.GetValue<string>("Storage:Provider") ?? "EfCore";

        Log.Information("Storage Provider: {Provider}", provider);
        
        if (provider.Equals("File", StringComparison.OrdinalIgnoreCase))
        {
            var fileOptions = configuration.GetSection("Storage:File").Get<FileReportStoreOptions>();
            if (fileOptions != null)
            {
                Log.Information("File Storage Base Path: {BasePath}", fileOptions.BasePath);
            }
            return sp.GetRequiredService<FileReportStore>();
        }
        else
        {
            return sp.GetRequiredService<EfCoreReportStore>();
        }
    });

    // Configure Export Service
    Log.Information("Configuring Export Service...");
    builder.Services.AddScoped<IExportService, ExportService>();

    // Configure Certificate Update Service
    Log.Information("Configuring Certificate Update Service...");
    var updateConfig = builder.Configuration.GetSection("CertificateUpdateService");
    var updateEnabled = updateConfig.GetValue<bool>("Enabled");
    Log.Information("Certificate Update Service Enabled: {Enabled}", updateEnabled);
    
    if (updateEnabled)
    {
        var updateQueueUri = updateConfig.GetValue<string>("QueueServiceUri");
        var updateQueueName = updateConfig.GetValue<string>("CommandQueueName");
        
        Log.Information("  Command Queue URI: {QueueUri}", updateQueueUri);
        Log.Information("  Command Queue Name: {QueueName}", updateQueueName);
        Log.Information("  Auth Method is configured.");
    }
    
    builder.Services.Configure<CertificateUpdateServiceOptions>(updateConfig);
    builder.Services.AddScoped<ICertificateUpdateService, CertificateUpdateService>();

    // Configure Azure Queue Processor
    Log.Information("Configuring Queue Processor...");
    var queueConfig = builder.Configuration.GetSection("QueueProcessor");
    var queueEnabled = queueConfig.GetValue<bool>("Enabled");
    Log.Information("Queue Processor Enabled: {Enabled}", queueEnabled);
    
    if (queueEnabled)
    {
        var queueUri = queueConfig.GetValue<string>("QueueServiceUri");
        var queueName = queueConfig.GetValue<string>("QueueName");
        var authMethod = queueConfig.GetValue<string>("AuthenticationMethod");
        
        Log.Information("  Queue URI: {QueueUri}", queueUri);
        Log.Information("  Queue Name: {QueueName}", queueName);
        Log.Information("  Auth Method: {AuthMethod}", authMethod);
    }
    
    builder.Services.Configure<QueueProcessorOptions>(queueConfig);
    builder.Services.AddHostedService<QueueProcessorService>();

    Log.Information("Building WebApplication...");
    var app = builder.Build();

    // Log URLs configuration
    var urls = builder.Configuration["Urls"];
    if (!string.IsNullOrEmpty(urls))
    {
        Log.Information("Configured URLs: {Urls}", urls);
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        Log.Information("Swagger enabled at: /swagger");
    }

    app.UseHttpsRedirection();

    // Enable CORS before routing
    app.UseCors("AllowWebApp");
    Log.Information("CORS middleware enabled");

    app.MapControllers();
    app.MapHealthChecks("/health");
    
    // Map SignalR hub
    app.MapHub<DashboardHub>("/dashboardHub");
    Log.Information("SignalR DashboardHub mapped at: /dashboardHub");

    Log.Information("========================================");
    Log.Information("SecureBootDashboard.Api started successfully");
    Log.Information("========================================");

    app.Run();
}
catch (HostAbortedException)
{
    // Host was aborted during startup - this is usually caused by configuration errors
    Log.Fatal("Host was aborted during startup. Check configuration and dependencies.");
    Log.Information("Common causes:");
    Log.Information("  1. Invalid SQL Server connection string or database not accessible");
    Log.Information("  2. Missing or invalid Azure Queue configuration");
    Log.Information("  3. Certificate not found or not accessible");
    Log.Information("  4. Port already in use");
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "SecureBootDashboard.Api terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("Application shutting down...");
    Log.CloseAndFlush();
}

static string MaskConnectionString(string connectionString)
{
    // Mask password in connection string
    var parts = connectionString.Split(';');
    var masked = new List<string>();
    
    foreach (var part in parts)
    {
        if (part.Trim().StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
            part.Trim().StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase))
        {
            masked.Add("Password=***MASKED***");
        }
        else
        {
            masked.Add(part);
        }
    }
    
    return string.Join(";", masked);
}
