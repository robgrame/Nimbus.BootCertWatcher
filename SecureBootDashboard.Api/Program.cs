using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;
using SecureBootDashboard.Api.Storage;
using SecureBootWatcher.Shared.Storage;
using Serilog;
using Serilog.Events;

// Configure Serilog before building the app
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting SecureBootDashboard.Api application");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddDbContext<SecureBootDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

    builder.Services.AddHealthChecks();

    builder.Services.Configure<FileReportStoreOptions>(builder.Configuration.GetSection("Storage:File"));
    builder.Services.AddScoped<EfCoreReportStore>();
    builder.Services.AddScoped<FileReportStore>();
    builder.Services.AddScoped<IReportStore>(sp =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var provider = configuration.GetValue<string>("Storage:Provider") ?? "EfCore";

        return provider.Equals("File", StringComparison.OrdinalIgnoreCase)
            ? sp.GetRequiredService<FileReportStore>()
            : sp.GetRequiredService<EfCoreReportStore>();
    });

    // Configure Azure Queue Processor
    builder.Services.Configure<QueueProcessorOptions>(builder.Configuration.GetSection("QueueProcessor"));
    builder.Services.AddHostedService<QueueProcessorService>();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("SecureBootDashboard.Api started successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SecureBootDashboard.Api terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
