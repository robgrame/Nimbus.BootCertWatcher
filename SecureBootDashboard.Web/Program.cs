using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Hubs;
using SecureBootDashboard.Api.Services;
using SecureBootDashboard.Api.Storage;
using SecureBootDashboard.Web.Services;
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
using Azure.Identity;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization;

// Configure SqlClient to use managed networking stack (avoids native SNI platform issues)
AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

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
var serilogFilePath = tempConfig["Serilog:WriteTo:1:Args:path"] ?? "logs/unified-.log";
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
    Log.Information("Starting SecureBootDashboard Unified Application");
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
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            options.ConnectionString = appInsightsConnectionString;
        }
        options.EnableAdaptiveSampling = true;
        options.EnableDependencyTrackingTelemetryModule = true;
        options.EnablePerformanceCounterCollectionModule = true;
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

    // Configure Configuration Source Options
    Log.Information("Configuring Configuration Source Options...");
    builder.Services.Configure<ConfigurationSourceOptions>(builder.Configuration.GetSection(ConfigurationSourceOptions.SectionName));
    var configSourceOptions = builder.Configuration.GetSection(ConfigurationSourceOptions.SectionName).Get<ConfigurationSourceOptions>() ?? new ConfigurationSourceOptions();
    Log.Information("========================================");
    Log.Information("Configuration Source: {Provider}", configSourceOptions.Provider);
    Log.Information("  Use Database Configuration: {UseDatabase}", configSourceOptions.UseDatabaseConfiguration);
    Log.Information("  Use AppSettings Configuration: {UseAppSettings}", configSourceOptions.UseAppSettingsConfiguration);
    Log.Information("========================================");

    // Add Controllers (API) and Razor Pages (Web)
    builder.Services.AddControllers();
    builder.Services.AddRazorPages();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Add CORS (keep for backward compatibility, though not strictly needed for unified app)
    Log.Information("Configuring CORS...");
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWebApp", policy =>
        {
            // In unified app, CORS is mainly for external clients or backward compatibility
            var webAppUrl = builder.Configuration.GetValue<string>("WebAppUrl") ?? "https://localhost:7001";
            var alternativeUrls = builder.Configuration.GetSection("AlternativeWebUrls").Get<string[]>() ?? Array.Empty<string>();
            
            var allowedOrigins = new List<string> { webAppUrl };
            allowedOrigins.AddRange(alternativeUrls);
            
            policy.WithOrigins(allowedOrigins.ToArray())
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
            
            Log.Information("CORS configured for origins: {Origins}", string.Join(", ", allowedOrigins));
        });
    });

    // Add SignalR for real-time updates
    Log.Information("Configuring SignalR...");
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
        options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
        options.MaximumReceiveMessageSize = null;
    });
    Log.Information("SignalR configured successfully");

    // Configure Mutual TLS Authentication for API endpoints
    Log.Information("Configuring Mutual TLS Authentication...");
    builder.Services.Configure<MutualTlsOptions>(builder.Configuration.GetSection("MutualTls"));
    var mtlsConfig = builder.Configuration.GetSection("MutualTls").Get<MutualTlsOptions>();
    
    if (mtlsConfig?.Enabled == true)
    {
        Log.Information("Mutual TLS is ENABLED for API endpoints");
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
                        
                        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                        var configSourceOpts = configuration.GetSection(ConfigurationSourceOptions.SectionName)
                            .Get<ConfigurationSourceOptions>() ?? new ConfigurationSourceOptions();
                        
                        if (configSourceOpts.UseDatabaseConfiguration)
                        {
                            try
                            {
                                var certValidationService = context.HttpContext.RequestServices
                                    .GetService<ICertificateValidationService>();
                                
                                if (certValidationService != null)
                                {
                                    var dbConfig = await certValidationService.GetConfigurationAsync(context.HttpContext.RequestAborted);
                                    
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
                                        return;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Database certificate validation failed, falling back to appsettings.json");
                            }
                        }
                        else
                        {
                            Log.Debug("Database configuration disabled. Using appsettings.json for certificate validation.");
                        }
                        
                        Log.Debug("Using appsettings.json certificate validation");
                        
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
        Log.Information("Mutual TLS is DISABLED");
    }

    // Configure Web Authentication (EntraId, Windows, Anonymous)
    var authProvider = builder.Configuration["Authentication:Provider"];
    var isAppService = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
    Log.Information("Web Auth Provider: {Provider} (AppService={IsAppService})", authProvider ?? "None", isAppService);

    var useAnonymous = string.IsNullOrEmpty(authProvider) || string.Equals(authProvider, "None", StringComparison.OrdinalIgnoreCase);

    if (!useAnonymous && mtlsConfig?.Enabled != true)
    {
        if (string.Equals(authProvider, "EntraId", StringComparison.OrdinalIgnoreCase))
        {
            var clientId = builder.Configuration["Authentication:EntraId:ClientId"];
            var tenantId = builder.Configuration["Authentication:EntraId:TenantId"];

            if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(tenantId))
            {
                builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("Authentication:EntraId"));
                Log.Information("Entra ID configured for Web");
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
                Log.Information("Windows auth configured for Web");
            }
        }
        else
        {
            builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();
            Log.Information("Cookie auth configured for Web");
        }
    }

    // Authorization configuration
    if (mtlsConfig?.Enabled != true)
    {
        builder.Services.AddAuthorization(options =>
        {
            if (useAnonymous)
            {
                var allowAll = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();
                options.DefaultPolicy = allowAll;
                options.FallbackPolicy = allowAll;
            }
        });
    }

    // Configure Database
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
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
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
        }
        else
        {
            Log.Information("SQL Server connection string optimization skipped on {OS} platform", 
                RuntimeInformation.OSDescription);
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to configure connection string with SqlConnectionStringBuilder. Using original connection string. Error: {ErrorMessage}", ex.Message);
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
        
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
    });

    // Configure Health Checks
    Log.Information("Configuring Health Checks...");
    var healthCheckBuilder = builder.Services.AddHealthChecks();
    
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        try
        {
            healthCheckBuilder.AddSqlServer(
                connectionString: connectionString ?? throw new InvalidOperationException("SQL Server connection string is required"),
                name: "database",
                timeout: TimeSpan.FromSeconds(5),
                tags: new[] { "db", "sql", "sqlserver" });
            Log.Information("Health checks configured with SQL Server connectivity check");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to add SQL Server health check. Continuing without database health check.");
        }
    }
    else
    {
        Log.Warning("SQL Server health check not available on {OS} platform. Database health check will be skipped.", 
            RuntimeInformation.OSDescription);
    }

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
                options.AddBasePolicy(builder =>
                {
                    builder.Expire(TimeSpan.FromSeconds(30));
                });
                
                options.AddPolicy("DeviceList", builder =>
                {
                    builder.Expire(TimeSpan.FromSeconds(perfConfig.OutputCaching.DeviceListCacheDuration))
                           .SetVaryByQuery("*")
                           .Tag("devices");
                });
                
                options.AddPolicy("DeviceDetails", builder =>
                {
                    builder.Expire(TimeSpan.FromSeconds(perfConfig.OutputCaching.DeviceDetailsCacheDuration))
                           .SetVaryByRouteValue("id");
                });
                
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
            options.AddSlidingWindowLimiter("api", limiterOptions =>
            {
                limiterOptions.PermitLimit = perfConfig.RateLimiting.PermitLimit;
                limiterOptions.Window = TimeSpan.FromSeconds(perfConfig.RateLimiting.WindowSeconds);
                limiterOptions.SegmentsPerWindow = 4;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = perfConfig.RateLimiting.QueueLimit;
            });
            
            options.AddConcurrencyLimiter("concurrent", limiterOptions =>
            {
                limiterOptions.PermitLimit = perfConfig.RateLimiting.ConcurrencyLimit;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = perfConfig.RateLimiting.QueueLimit;
            });
            
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

    // Configure Storage services
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

    // Configure Windows Version Service
    Log.Information("Configuring Windows Version Service...");
    
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
    
    Log.Information("Office Versions API Client registered");
    
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

    // Register API Services
    builder.Services.AddScoped<ISecureBootReadinessService, SecureBootReadinessService>();
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<IApplicationSettingsService, ApplicationSettingsService>();
    builder.Services.AddScoped<IApiConfigurationService, ApiConfigurationService>();
    builder.Services.AddScoped<ICertificateValidationService, CertificateValidationService>();
    
    // Configure HTTP client for Web pages to access API endpoints on same application
    // In unified mode, this points to localhost (same app) to reuse existing page code
    Log.Information("Configuring HTTP client for API access...");
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
    
    // Update ApiSettings to point to same application (will be set via appsettings or default to current URL)
    builder.Services.Configure<ApiSettings>(options =>
    {
        // In unified mode, API is on the same base URL
        var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl))
        {
            // Default to localhost on the same port as the unified app
            options.BaseUrl = "https://localhost:7001";
        }
    });
    Log.Information("API client configured to use: {BaseUrl}", builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001");

    // Configure Azure Queue Processor
    Log.Information("Configuring Queue Processor...");
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

    // Configure middleware pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        Log.Information("Swagger enabled at: /swagger");
    }
    else
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
    
    // Enable Response Compression (before CORS and routing)
    if (perfConfig?.Compression?.Enabled == true)
    {
        app.UseResponseCompression();
        Log.Information("Response Compression middleware enabled");
    }

    // Enable CORS before routing
    app.UseCors("AllowWebApp");
    Log.Information("CORS middleware enabled");
    
    // Static files for Razor Pages
    app.UseStaticFiles();
    
    // Routing
    app.UseRouting();
    
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

    // Authentication & Authorization
    if (mtlsConfig?.Enabled == true || !useAnonymous)
    {
        app.UseAuthentication();
        Log.Information("Authentication middleware enabled");
    }
    
    app.UseAuthorization();
    Log.Information("Authorization middleware enabled");

    // Map endpoints
    app.MapControllers();
    app.MapRazorPages();
    
    // Health checks
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    
    // SignalR hub
    app.MapHub<DashboardHub>("/dashboardHub");
    Log.Information("SignalR DashboardHub mapped at: /dashboardHub");

    Log.Information("========================================");
    Log.Information("SecureBootDashboard Unified Application started successfully");
    Log.Information("  Web Dashboard: https://localhost:7001");
    Log.Information("  API Endpoints: https://localhost:7001/api/*");
    Log.Information("  SignalR Hub: https://localhost:7001/dashboardHub");
    Log.Information("  Health Check: https://localhost:7001/health");
    if (app.Environment.IsDevelopment())
    {
        Log.Information("  Swagger UI: https://localhost:7001/swagger");
    }
    Log.Information("  Auth Mode: {Mode}", useAnonymous ? "Anonymous" : authProvider ?? "Cookie");
    if (mtlsConfig?.Enabled == true)
    {
        Log.Information("  MTLS: Enabled for API endpoints");
    }
    Log.Information("========================================");

    app.Run();
}
catch (HostAbortedException ex)
{
    Log.Fatal(ex, "Host was aborted during startup. Check configuration and dependencies.");
    Log.Information("Common causes:");
    Log.Information("  1. Invalid SQL Server connection string or database not accessible");
    Log.Information("  2. Missing or invalid Azure Queue configuration");
    Log.Information("  3. Certificate not found or not accessible");
    Log.Information("  4. Port already in use");
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "SecureBootDashboard Unified Application terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("Application shutting down...");
    Log.CloseAndFlush();
}

static string MaskConnectionString(string connectionString)
{
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
