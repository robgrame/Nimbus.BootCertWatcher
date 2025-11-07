using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.GraphQL.Queries;
using SecureBootDashboard.Api.GraphQL.Mutations;
using SecureBootDashboard.Api.Services;
using SecureBootDashboard.Api.Storage;
using SecureBootWatcher.Shared.Storage;
using Serilog;
using Serilog.Events;

// Configure Serilog before building the app
var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "logs", "api-.log");
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
    Log.Information("Log Path: {LogPath}", System.IO.Path.GetFullPath(logPath));
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

    // Configure GraphQL
    Log.Information("Configuring GraphQL...");
    builder.Services
        .AddGraphQLServer()
        .AddQueryType<Query>()
        .AddMutationType<Mutation>();

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

    app.MapControllers();
    app.MapGraphQL();
    Log.Information("GraphQL endpoint enabled at: /graphql");
    app.MapHealthChecks("/health");

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
