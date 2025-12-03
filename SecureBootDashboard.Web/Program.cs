using Microsoft.Identity.Web;
using SecureBootDashboard.Web.Services;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Reflection;

    // Configure Serilog before building the app
    var workspaceRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName ?? AppContext.BaseDirectory;
    var logPath = Path.Combine(workspaceRoot, "logs", "web-.log");
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
        Log.Information("Base Directory: {BaseDirectory}", AppContext.BaseDirectory);
        Log.Information("Log File Path: {LogPath}", Path.GetFullPath(logPath));
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
                        }
                        // Otherwise try to load from file
                        else if (!string.IsNullOrEmpty(apiSettings.CertificatePath))
                        {
                            Log.Information("Loading API client certificate from file: {Path}", apiSettings.CertificatePath);
                            
                            if (!string.IsNullOrEmpty(apiSettings.CertificatePassword))
                            {
                                certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                                    apiSettings.CertificatePath,
                                    apiSettings.CertificatePassword);
                            }
                            else
                            {
                                certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
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
