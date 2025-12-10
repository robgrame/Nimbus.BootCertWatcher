using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Hubs;
using SecureBootDashboard.Api.Services;
using SecureBootDashboard.Api.Storage;
using SecureBootWatcher.Shared.Storage;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.Certificate;
using System.Security.Cryptography.X509Certificates;
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
Console.WriteLine($"Config Microsoft: {tempConfig["Logging:LogLevel:Microsoft"] ?? "NOT SET"}");
Console.WriteLine($"Config Microsoft.AspNetCore: {tempConfig["Logging:LogLevel:Microsoft.AspNetCore"] ?? "NOT SET"}");

// Read Serilog file path from configuration
var serilogFilePath = tempConfig["Serilog:WriteTo:1:Args:path"] ?? "logs/api-.log";
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
    Log.Information("Starting SecureBootDashboard.Api");
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

    // Configure and log Configuration Source Options
    Log.Information("Configuring Configuration Source Options...");
    builder.Services.Configure<ConfigurationSourceOptions>(builder.Configuration.GetSection(ConfigurationSourceOptions.SectionName));
    var configSourceOptions = builder.Configuration.GetSection(ConfigurationSourceOptions.SectionName).Get<ConfigurationSourceOptions>() ?? new ConfigurationSourceOptions();
    Log.Information("========================================");
    Log.Information("Configuration Source: {Provider}", configSourceOptions.Provider);
    Log.Information("  Use Database Configuration: {UseDatabase}", configSourceOptions.UseDatabaseConfiguration);
    Log.Information("  Use AppSettings Configuration: {UseAppSettings}", configSourceOptions.UseAppSettingsConfiguration);
    Log.Information("========================================");

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
        // Increase timeouts to prevent premature disconnections
        // KeepAlive: How often server sends ping to client (default: 15s)
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
        // ClientTimeout: How long server waits before considering client disconnected (default: 30s)
        // Should be at least 2x KeepAliveInterval
        options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
        // HandshakeTimeout: Timeout for initial connection handshake (default: 15s)
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
        // MaximumReceiveMessageSize: Maximum message size (null = unlimited)
        options.MaximumReceiveMessageSize = null;
    });
    Log.Information("SignalR configured successfully");

    // Configure Mutual TLS Authentication
    Log.Information("Configuring Mutual TLS Authentication...");
    builder.Services.Configure<MutualTlsOptions>(builder.Configuration.GetSection("MutualTls"));
    var mtlsConfig = builder.Configuration.GetSection("MutualTls").Get<MutualTlsOptions>();
    
    if (mtlsConfig?.Enabled == true)
    {
        Log.Information("Mutual TLS is ENABLED");
        Log.Information("  Allow Self-Signed Certificates: {AllowSelfSigned}", mtlsConfig.AllowSelfSignedCertificates);
        Log.Information("  Check Certificate Revocation: {CheckRevocation}", mtlsConfig.CheckCertificateRevocation);
        Log.Information("  Validate Certificate Chain: {ValidateChain}", mtlsConfig.ValidateCertificateChain);
        
        if (mtlsConfig.AllowedThumbprints?.Count > 0)
        {
            Log.Information("  Allowed Thumbprints: {Count} configured", mtlsConfig.AllowedThumbprints.Count);
            foreach (var thumbprint in mtlsConfig.AllowedThumbprints)
            {
                Log.Information("    - {Thumbprint}", thumbprint);
            }
        }
        
        if (mtlsConfig.AllowedIssuers?.Count > 0)
        {
            Log.Information("  Allowed Issuers: {Count} configured", mtlsConfig.AllowedIssuers.Count);
            foreach (var issuer in mtlsConfig.AllowedIssuers)
            {
                Log.Information("    - {Issuer}", issuer);
            }
        }
        
        builder.Services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
            .AddCertificate(options =>
            {
                options.AllowedCertificateTypes = mtlsConfig.AllowSelfSignedCertificates 
                    ? CertificateTypes.All 
                    : CertificateTypes.Chained;
                
                options.RevocationMode = mtlsConfig.CheckCertificateRevocation 
                    ? X509RevocationMode.Online 
                    : X509RevocationMode.NoCheck;
                
                options.ValidateCertificateUse = true;
                options.ValidateValidityPeriod = true;
                
                options.Events = new CertificateAuthenticationEvents
                {
                    OnCertificateValidated = async context =>
                    {
                        var certificate = context.ClientCertificate;
                        Log.Debug("Certificate validation requested for: {Subject}", certificate.Subject);
                        
                        // Check if database configuration is enabled
                        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                        var configSourceOpts = configuration.GetSection(ConfigurationSourceOptions.SectionName)
                            .Get<ConfigurationSourceOptions>() ?? new ConfigurationSourceOptions();
                        
                        // Try database-driven validation first (if database configuration is enabled)
                        if (configSourceOpts.UseDatabaseConfiguration)
                        {
                            try
                            {
                                var certValidationService = context.HttpContext.RequestServices
                                    .GetService<ICertificateValidationService>();
                                
                                if (certValidationService != null)
                                {
                                    var dbConfig = await certValidationService.GetConfigurationAsync(context.HttpContext.RequestAborted);
                                    
                                    // Use database validation if enabled
                                    if (dbConfig?.Enabled == true)
                                    {
                                        Log.Information("Using database-driven certificate validation");
                                        
                                        var validationResult = await certValidationService.ValidateClientCertificateAsync(
                                            certificate,
                                            context.HttpContext.RequestAborted);
                                        
                                        if (!validationResult.IsValid)
                                        {
                                            var errorMessage = string.Join("; ", validationResult.Errors);
                                            Log.Warning("Certificate validation failed (database): {Errors}", errorMessage);
                                            context.Fail(errorMessage);
                                            return;
                                        }
                                        
                                        if (validationResult.Warnings.Any())
                                        {
                                            Log.Warning("Certificate validation warnings: {Warnings}", 
                                                string.Join("; ", validationResult.Warnings));
                                        }
                                        
                                        Log.Information("Certificate validated successfully via database (Matched CA: {CA})", 
                                            validationResult.MatchedCA?.CommonName ?? "None");
                                        
                                        context.Success();
                                        return; // Exit early - database validation completed
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Database certificate validation failed, falling back to appsettings.json");
                                // Continue with appsettings.json validation below
                            }
                        }
                        else
                        {
                            Log.Debug("Database configuration disabled. Using appsettings.json for certificate validation.");
                        }
                        
                        // Fallback to appsettings.json validation (original behavior)
                        Log.Debug("Using appsettings.json certificate validation");
                        
                        // Check thumbprint allowlist if configured
                        if (mtlsConfig.AllowedThumbprints?.Count > 0)
                        {
                            var thumbprint = certificate.Thumbprint;
                            if (!mtlsConfig.AllowedThumbprints.Contains(thumbprint, StringComparer.OrdinalIgnoreCase))
                            {
                                Log.Warning("Certificate rejected - thumbprint not in allowlist: {Thumbprint}", thumbprint);
                                context.Fail("Certificate thumbprint not allowed");
                                return;
                            }
                            Log.Debug("Certificate thumbprint validated: {Thumbprint}", thumbprint);
                        }
                        
                        // Check issuer allowlist if configured
                        if (mtlsConfig.AllowedIssuers?.Count > 0)
                        {
                            var issuerCN = certificate.Issuer;
                            var issuerAllowed = mtlsConfig.AllowedIssuers.Any(allowed => 
                                issuerCN.Contains($"CN={allowed}", StringComparison.OrdinalIgnoreCase));
                            
                            if (!issuerAllowed)
                            {
                                Log.Warning("Certificate rejected - issuer not in allowlist: {Issuer}", issuerCN);
                                context.Fail("Certificate issuer not allowed");
                                return;
                            }
                            Log.Debug("Certificate issuer validated: {Issuer}", issuerCN);
                        }
                        
                        Log.Information("Certificate validated successfully: Subject={Subject}, Issuer={Issuer}, Thumbprint={Thumbprint}", 
                            certificate.Subject, certificate.Issuer, certificate.Thumbprint);
                        
                        context.Success();
                    },
                    OnAuthenticationFailed = context =>
                    {
                        Log.Error(context.Exception, "Certificate authentication failed");
                        return Task.CompletedTask;
                    }
                };
            });
        
        builder.Services.AddAuthorization();
        Log.Information("Mutual TLS authentication configured successfully");
    }
    else
    {
        Log.Information("Mutual TLS is DISABLED - API endpoints will not require client certificates");
    }

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
    
    // Configure Performance Options
    Log.Information("Configuring Performance Options...");
    builder.Services.Configure<PerformanceOptions>(builder.Configuration.GetSection("Performance"));
    var perfConfig = builder.Configuration.GetSection("Performance").Get<PerformanceOptions>();
    
    // Build connection string with performance settings
    var connectionStringBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
    if (perfConfig?.Database != null)
    {
        connectionStringBuilder.MaxPoolSize = perfConfig.Database.MaxPoolSize;
        connectionStringBuilder.MinPoolSize = perfConfig.Database.MinPoolSize;
        connectionStringBuilder.ConnectTimeout = perfConfig.Database.CommandTimeout;
        connectionStringBuilder.MultipleActiveResultSets = true;
        connectionString = connectionStringBuilder.ConnectionString;
        
        Log.Information("Database Performance Settings:");
        Log.Information("  Max Pool Size: {MaxPoolSize}", perfConfig.Database.MaxPoolSize);
        Log.Information("  Min Pool Size: {MinPoolSize}", perfConfig.Database.MinPoolSize);
        Log.Information("  Command Timeout: {CommandTimeout}s", perfConfig.Database.CommandTimeout);
    }
    
    builder.Services.AddDbContext<SecureBootDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.CommandTimeout(perfConfig?.Database?.CommandTimeout ?? 30);
            if (perfConfig?.Database?.EnableQuerySplitting == true)
            {
                sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            }
        });
        
        // Disable sensitive data logging in production
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
    });

    // Configure Health Checks with database connectivity
    Log.Information("Configuring Health Checks...");
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            connectionString: connectionString ?? throw new InvalidOperationException("SQL Server connection string is required"),
            name: "database",
            timeout: TimeSpan.FromSeconds(5),
            tags: new[] { "db", "sql", "sqlserver" });
    Log.Information("Health checks configured with SQL Server connectivity check");

    // Configure Response Compression
    if (perfConfig?.Compression?.Enabled == true)
    {
        Log.Information("Configuring Response Compression...");
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        });
        
        builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = perfConfig.Compression.Level switch
            {
                "Fastest" => CompressionLevel.Fastest,
                "SmallestSize" => CompressionLevel.SmallestSize,
                _ => CompressionLevel.Optimal
            };
        });
        
        builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
        {
            options.Level = perfConfig.Compression.Level switch
            {
                "Fastest" => CompressionLevel.Fastest,
                "SmallestSize" => CompressionLevel.SmallestSize,
                _ => CompressionLevel.Optimal
            };
        });
        
        Log.Information("  Compression Level: {Level}", perfConfig.Compression.Level);
    }

    // Configure Output Caching
    if (perfConfig?.OutputCaching?.Enabled == true)
    {
        Log.Information("Configuring Output Caching...");
        if (perfConfig.OutputCaching.UseRedis && !string.IsNullOrEmpty(perfConfig.OutputCaching.RedisConnectionString))
        {
            Log.Information("  Using Redis distributed cache");
            builder.Services.AddStackExchangeRedisOutputCache(options =>
            {
                options.Configuration = perfConfig.OutputCaching.RedisConnectionString;
            });
        }
        else
        {
            Log.Information("  Using in-memory cache");
            builder.Services.AddOutputCache(options =>
            {
                // Default policy for most endpoints
                options.AddBasePolicy(builder =>
                {
                    builder.Expire(TimeSpan.FromSeconds(30));
                });
                
                // Device list policy
                options.AddPolicy("DeviceList", builder =>
                {
                    builder.Expire(TimeSpan.FromSeconds(perfConfig.OutputCaching.DeviceListCacheDuration))
                           .SetVaryByQuery("*")
                           .Tag("devices");
                });
                
                // Device details policy
                options.AddPolicy("DeviceDetails", builder =>
                {
                    builder.Expire(TimeSpan.FromSeconds(perfConfig.OutputCaching.DeviceDetailsCacheDuration))
                           .SetVaryByRouteValue("id");
                });
                
                // Statistics policy
                options.AddPolicy("Statistics", builder =>
                {
                    builder.Expire(TimeSpan.FromSeconds(perfConfig.OutputCaching.StatisticsCacheDuration));
                });
            });
        }
    }

    // Configure Rate Limiting
    if (perfConfig?.RateLimiting?.Enabled == true)
    {
        Log.Information("Configuring Rate Limiting...");
        builder.Services.AddRateLimiter(options =>
        {
            // Sliding window rate limiter for API endpoints
            options.AddSlidingWindowLimiter("api", limiterOptions =>
            {
                limiterOptions.PermitLimit = perfConfig.RateLimiting.PermitLimit;
                limiterOptions.Window = TimeSpan.FromSeconds(perfConfig.RateLimiting.WindowSeconds);
                limiterOptions.SegmentsPerWindow = 4;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = perfConfig.RateLimiting.QueueLimit;
            });
            
            // Concurrency limiter for expensive operations
            options.AddConcurrencyLimiter("concurrent", limiterOptions =>
            {
                limiterOptions.PermitLimit = perfConfig.RateLimiting.ConcurrencyLimit;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = perfConfig.RateLimiting.QueueLimit;
            });
            
            // Higher limits for health checks
            options.AddFixedWindowLimiter("health", limiterOptions =>
            {
                limiterOptions.PermitLimit = 100;
                limiterOptions.Window = TimeSpan.FromSeconds(1);
            });
            
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });
        
        Log.Information("  Permit Limit: {PermitLimit} per {WindowSeconds}s", 
            perfConfig.RateLimiting.PermitLimit, 
            perfConfig.RateLimiting.WindowSeconds);
        Log.Information("  Concurrency Limit: {ConcurrencyLimit}", 
            perfConfig.RateLimiting.ConcurrencyLimit);
        Log.Information("  Queue Limit: {QueueLimit}", 
            perfConfig.RateLimiting.QueueLimit);
    }

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

    // Configure Windows Version Service (Configuration-based, no WindowsVersionsCore dependency)
    Log.Information("Configuring Windows Version Service...");
    
    // Register Office Versions API Client
    builder.Services.AddHttpClient<IOfficeVersionsApiClient, OfficeVersionsApiClient>(client =>
    {
        client.BaseAddress = new Uri("https://officeversions.azurewebsites.net/api");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "SecureBootDashboard/1.14");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    });
    
    Log.Information("Office Versions API Client registered (Primary source: officeversions.azurewebsites.net)");
    
    builder.Services.AddScoped<SecureBootDashboard.Api.Services.IWindowsVersionService, SecureBootDashboard.Api.Services.WindowsVersionService>();

    // Configure Windows Security Options
    Log.Information("Configuring Windows Security Options...");
    builder.Services.Configure<WindowsSecurityOptions>(builder.Configuration.GetSection("WindowsSecurity"));
    var windowsSecurityConfig = builder.Configuration.GetSection("WindowsSecurity").Get<WindowsSecurityOptions>();
    if (windowsSecurityConfig != null)
    {
        Log.Information("Windows Security Configuration:");
        Log.Information("  Firmware Security Date: {Date}", windowsSecurityConfig.FirmwareSecurityDate.ToString("yyyy-MM-dd"));
        Log.Information("  Minimum Secure Builds: {Count}", windowsSecurityConfig.MinimumSecureBuilds.Count);
        foreach (var kvp in windowsSecurityConfig.MinimumSecureBuilds)
        {
            Log.Information("    {Version}: {BuildNumber} ({Name})", kvp.Key, kvp.Value.BuildNumber, kvp.Value.Name);
        }
    }

    // Configure Secure Boot Readiness Options
    Log.Information("Configuring Secure Boot Readiness Options...");
    builder.Services.Configure<SecureBootReadinessOptions>(builder.Configuration.GetSection("SecureBootReadiness"));
    var readinessConfig = builder.Configuration.GetSection("SecureBootReadiness").Get<SecureBootReadinessOptions>();
    if (readinessConfig != null)
    {
        Log.Information("Secure Boot Readiness Configuration:");
        Log.Information("  Certificate Expiration Warning Days: {Days}", readinessConfig.CertificateExpirationWarningDays);
        Log.Information("  Certificate Expiration Critical Days: {Days}", readinessConfig.CertificateExpirationCriticalDays);
        Log.Information("  Require Windows UEFI CA 2023: {Required}", readinessConfig.RequireWindowsUEFICA2023);
        Log.Information("  Windows UEFI CA 2023 Thumbprint: {Thumbprint}", readinessConfig.WindowsUEFICA2023Thumbprint);
        Log.Information("  Require OEM Certificates Valid: {Required}", readinessConfig.RequireOemCertificatesValid);
        Log.Information("  Minimum OS Build Versions: {Count}", readinessConfig.MinimumOSBuildVersions.Count);
        foreach (var kvp in readinessConfig.MinimumOSBuildVersions)
        {
            Log.Information("    {OS}: {Build}", kvp.Key, kvp.Value);
        }
    }

    // Register Secure Boot Readiness Service
    builder.Services.AddScoped<ISecureBootReadinessService, SecureBootReadinessService>();

    // Configure Application Settings Service (database-driven configuration)
    Log.Information("Configuring Application Settings Service...");
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<IApplicationSettingsService, ApplicationSettingsService>();

    // Configure API Configuration Service (database-driven API settings)
    Log.Information("Configuring API Configuration Service...");
    builder.Services.AddScoped<IApiConfigurationService, ApiConfigurationService>();

    // Configure Certificate Validation Service (for mutual TLS)
    Log.Information("Configuring Certificate Validation Service...");
    builder.Services.AddScoped<ICertificateValidationService, CertificateValidationService>();

    // Configure Azure Queue Processor
    Log.Information("Configuring Queue Processor...");
    
    // Register Options Provider to load configuration from database with fallback to appsettings.json
    Log.Information("Registering Queue Processor Options Provider (Database ? appsettings.json)...");
    builder.Services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<QueueProcessorOptions>, QueueProcessorOptionsProvider>();
    
    var queueConfig = builder.Configuration.GetSection("QueueProcessor");
    var queueEnabled = queueConfig.GetValue<bool>("Enabled");
    Log.Information("Queue Processor Enabled (from appsettings.json): {Enabled}", queueEnabled);
    
    if (queueEnabled)
    {
        var queueUri = queueConfig.GetValue<string>("QueueServiceUri");
        var queueName = queueConfig.GetValue<string>("QueueName");
        var authMethod = queueConfig.GetValue<string>("AuthenticationMethod");
        
        Log.Information("  Queue URI (appsettings.json): {QueueUri}", queueUri);
        Log.Information("  Queue Name (appsettings.json): {QueueName}", queueName);
        Log.Information("  Auth Method (appsettings.json): {AuthMethod}", authMethod);
        Log.Information("  NOTE: These values may be overridden by database configuration if available");
    }
    
    // Load default configuration from appsettings.json
    // This will be overridden by QueueProcessorOptionsProvider if database config is available
    builder.Services.Configure<QueueProcessorOptions>(queueConfig);
    builder.Services.AddHostedService<QueueProcessorService>();

    // Configure Device Cleanup Service
    Log.Information("Configuring Device Cleanup Service...");
    builder.Services.AddHostedService<DeviceCleanupService>();

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
    
    // Enable Response Compression (before CORS and routing)
    if (perfConfig?.Compression?.Enabled == true)
    {
        app.UseResponseCompression();
        Log.Information("Response Compression middleware enabled");
    }

    // Enable CORS before routing
    app.UseCors("AllowWebApp");
    Log.Information("CORS middleware enabled");
    
    // Enable Rate Limiting
    if (perfConfig?.RateLimiting?.Enabled == true)
    {
        app.UseRateLimiter();
        Log.Information("Rate Limiting middleware enabled");
    }
    
    // Enable Output Caching
    if (perfConfig?.OutputCaching?.Enabled == true)
    {
        app.UseOutputCache();
        Log.Information("Output Caching middleware enabled");
    }

    // Enable Certificate Authentication if configured
    if (mtlsConfig?.Enabled == true)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        Log.Information("Certificate Authentication middleware enabled");
    }

    app.MapControllers();
    
    // Enhanced health checks with details
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    
    // Map SignalR hub
    app.MapHub<DashboardHub>("/dashboardHub");
    Log.Information("SignalR DashboardHub mapped at: /dashboardHub");

    Log.Information("========================================");
    Log.Information("SecureBootDashboard.Api started successfully");
    Log.Information("========================================");

    app.Run();
}
catch (HostAbortedException ex) // Add ex parameter
{
    // Host was aborted during startup - this is usually caused by configuration errors
    Log.Fatal(ex, "Host was aborted during startup. Check configuration and dependencies."); // Log the exception
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
