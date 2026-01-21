using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Client.Configuration;
using SecureBootWatcher.Client.Logging;
using SecureBootWatcher.Client.Services;
using SecureBootWatcher.Client.Sinks;
using SecureBootWatcher.Client.Storage;
using SecureBootWatcher.Shared.Configuration;
using Serilog;
using Serilog.Events;

namespace SecureBootWatcher.Client
{
	internal static class Program
	{
		private static async Task<int> Main(string[] args)
		{
			// Build configuration first to read logging settings
			var configuration = new ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
				.AddEnvironmentVariables(prefix: "SECUREBOOT_")
				.AddCommandLine(args)
				.Build();

			// Configure Serilog from configuration
			var logPath = configuration.GetValue<string>("Logging:File:Path") ?? Path.Combine(AppContext.BaseDirectory, "logs", "client-.log");
			var rollingIntervalString = configuration.GetValue<string>("Logging:File:RollingInterval") ?? "Day";
			var retainedFileCountLimit = configuration.GetValue<int?>("Logging:File:RetainedFileCountLimit") ?? 30;
			var fileSizeLimitBytes = configuration.GetValue<long?>("Logging:File:FileSizeLimitBytes");
			var rollOnFileSizeLimit = configuration.GetValue<bool>("Logging:File:RollOnFileSizeLimit");
			var logFormat = configuration.GetValue<string>("Logging:File:Format") ?? "CMTrace";
			var consoleEnabled = configuration.GetValue<bool?>("Logging:Console:Enabled") ?? true;
			
			// Parse RollingInterval enum (.NET Framework 4.8 compatible)
			RollingInterval rollingInterval;
			if (!Enum.TryParse(rollingIntervalString, true, out rollingInterval))
			{
				rollingInterval = RollingInterval.Day;
			}
			
			// Read minimum log level from configuration
			var minimumLevelString = configuration.GetValue<string>("Logging:LogLevel:Default") ?? "Information";
			
			// Map Microsoft.Extensions.Logging levels to Serilog levels
			LogEventLevel minimumLevel;
			if (minimumLevelString.Equals("Trace", StringComparison.OrdinalIgnoreCase))
			{
				minimumLevel = LogEventLevel.Verbose; // Trace -> Verbose in Serilog
			}
			else if (!Enum.TryParse(minimumLevelString, true, out minimumLevel))
			{
				minimumLevel = LogEventLevel.Information;
			}
			
			// Resolve log path relative to base directory if not absolute
			if (!Path.IsPathRooted(logPath))
			{
				logPath = Path.Combine(AppContext.BaseDirectory, logPath);
			}
			
			// Choose output template based on format setting
			string? fileOutputTemplate;
			Serilog.Formatting.ITextFormatter? textFormatter = null;
	
			if (logFormat.Equals("CMTrace", StringComparison.OrdinalIgnoreCase))
			{
				// Use custom CMTrace formatter for proper compatibility
				textFormatter = new CMTraceFormatter();
				fileOutputTemplate = null; // Not used with custom formatter
			}
			else
			{
				// Standard text format
				fileOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
			}
		
			var loggerConfig = new LoggerConfiguration()
				.MinimumLevel.Is(minimumLevel)  // Set minimum level from configuration
				.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
				.MinimumLevel.Override("System", LogEventLevel.Warning)
				.Enrich.FromLogContext()
				.Enrich.WithThreadId();
		
			// Add console sink if enabled
			if (consoleEnabled)
			{
				loggerConfig.WriteTo.Console(
					outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
			}
		
			// Add file sink with configuration
			if (textFormatter != null)
			{
				// Use custom formatter (CMTrace)
				if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes.Value > 0)
				{
					loggerConfig.WriteTo.File(
						textFormatter,
						path: logPath,
						rollingInterval: rollingInterval,
						retainedFileCountLimit: retainedFileCountLimit,
						fileSizeLimitBytes: fileSizeLimitBytes.Value,
						rollOnFileSizeLimit: rollOnFileSizeLimit);
				}
				else
				{
					loggerConfig.WriteTo.File(
						textFormatter,
						path: logPath,
						rollingInterval: rollingInterval,
						retainedFileCountLimit: retainedFileCountLimit);
				}
			}
			else
			{
				// Use output template (Standard format)
				if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes.Value > 0)
				{
					loggerConfig.WriteTo.File(
						path: logPath,
						rollingInterval: rollingInterval,
						retainedFileCountLimit: retainedFileCountLimit,
						fileSizeLimitBytes: fileSizeLimitBytes.Value,
						rollOnFileSizeLimit: rollOnFileSizeLimit,
						outputTemplate: fileOutputTemplate!);
				}
				else
				{
					loggerConfig.WriteTo.File(
						path: logPath,
						rollingInterval: rollingInterval,
						retainedFileCountLimit: retainedFileCountLimit,
						outputTemplate: fileOutputTemplate!);
				}
			}
			
			Log.Logger = loggerConfig.CreateLogger();

			using var cancellationSource = new CancellationTokenSource();
			
			try
			{
				// Get version info - prioritize AssemblyInformationalVersion for GitVersioning
				var assembly = Assembly.GetExecutingAssembly();
				var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
				
				string version;
				if (!string.IsNullOrWhiteSpace(informationalVersion))
				{
					// Remove commit hash (everything after '+') if present
					// Example: "1.1.1.48182+a1b2c3d" -> "1.1.1.48182"
					var plusIndex = informationalVersion!.IndexOf('+');
					version = plusIndex > 0 
						? informationalVersion.Substring(0, plusIndex) 
						: informationalVersion;
				}
				else
				{
					version = assembly.GetName().Version?.ToString() ?? "Unknown";
				}
				
				// Log startup information
				Log.Information("========================================");
				Log.Information("SecureBootWatcher Client Starting");
				Log.Information("========================================");
				Log.Information("Version: {Version}", version);
				Log.Information("Logging Level: {LogLevel}", minimumLevel);
				Log.Information("Base Directory: {BaseDirectory}", AppContext.BaseDirectory);
				Log.Information("Log File Path: {LogPath}", Path.GetFullPath(logPath));
				Log.Information("Log Format: {Format}", logFormat);
				Log.Information("Rolling Interval: {Interval}", rollingInterval);
				Log.Information("Retained File Count: {Count}", retainedFileCountLimit);
				if (fileSizeLimitBytes.HasValue)
				{
					Log.Information("File Size Limit: {Size} bytes ({SizeMB} MB)", fileSizeLimitBytes.Value, fileSizeLimitBytes.Value / 1024.0 / 1024.0);
					Log.Information("Roll On File Size Limit: {RollOnSize}", rollOnFileSizeLimit);
				}
				Log.Information("Console Enabled: {ConsoleEnabled}", consoleEnabled);
				Log.Information("Current Directory: {CurrentDirectory}", Environment.CurrentDirectory);
				Log.Information("Machine Name: {MachineName}", Environment.MachineName);
				Log.Information("Domain: {Domain}", Environment.UserDomainName);
				Log.Information("User: {User}", Environment.UserName);
				Log.Information(".NET Framework: {Framework}", Environment.Version);
				Log.Information("OS: {OS}", Environment.OSVersion);
				
				// Test debug logging
				if (minimumLevel <= LogEventLevel.Debug)
				{
					Log.Debug("Debug logging is ENABLED - you should see this message");
					Log.Verbose("Verbose logging is ENABLED - you should see this message");
				}

				Console.CancelKeyPress += (_, eventArgs) =>
				{
					eventArgs.Cancel = true;
					Log.Information("Cancellation requested (Ctrl+C)...");
					cancellationSource.Cancel();
				};

				// Configuration already built above
				
				// Log configuration file locations
				var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
				var appsettingsLocalPath = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
				
				Log.Information("Configuration Files:");
				Log.Information("  appsettings.json: {Exists}", File.Exists(appsettingsPath) ? "Found" : "Not Found");
				Log.Information("  appsettings.local.json: {Exists}", File.Exists(appsettingsLocalPath) ? "Found" : "Not Found");

				using var serviceProvider = BuildServices(configuration);

				var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<SecureBootWatcherOptions>>();
				var options = optionsMonitor.CurrentValue;
				
				LogConfiguration(options);

				var service = serviceProvider.GetRequiredService<SecureBootWatcherService>();
				await service.RunAsync(cancellationSource.Token).ConfigureAwait(false);
				
				Log.Information("========================================");
				Log.Information("SecureBootWatcher Client Stopped Successfully");
				Log.Information("========================================");
				return 0;
			}
			catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
			{
				Log.Information("SecureBootWatcher Client cancelled by user");
				return 0;
			}
			catch (Exception ex)
			{
				Log.Fatal(ex, "SecureBootWatcher Client terminated unexpectedly");
				return 1;
			}
			finally
			{
				Log.Information("Shutting down...");
				Log.CloseAndFlush();
			}
		}

		private static ServiceProvider BuildServices(IConfiguration configuration)
		{
			var services = new ServiceCollection();

			// Add Serilog as logging provider
			services.AddLogging(builder =>
			{
				builder.ClearProviders();
				builder.AddSerilog(dispose: false);
			});

			// Configure HttpClient with certificate support
			services.AddHttpClient("SecureBootIngestion")
				.ConfigurePrimaryHttpMessageHandler(sp =>
				{
					var options = sp.GetRequiredService<IOptionsMonitor<SecureBootWatcherOptions>>();
					var webApiOptions = options.CurrentValue.Sinks.WebApi;
					var azureFunctionOptions = options.CurrentValue.Sinks.AzureFunction;
					
					var handler = new System.Net.Http.HttpClientHandler();
					
					// Configure certificate authentication for WebApi sink if enabled
					if (webApiOptions.UseCertificateAuth)
					{
						var certificate = LoadAndValidateCertificate(
							webApiOptions.CertificateThumbprint,
							webApiOptions.CertificatePath,
							webApiOptions.CertificatePassword,
							webApiOptions.CertificateStoreLocation,
							webApiOptions.CertificateStoreName,
							webApiOptions.ValidateCertificateChain,
							webApiOptions.CheckCertificateRevocation,
							webApiOptions.ExpectedCARootName,
							webApiOptions.ExpectedCARootThumbprint,
							webApiOptions.ExpectedSubordinateCAs,
							"WebApi");
						
						if (certificate != null)
						{
							handler.ClientCertificates.Add(certificate);
							Log.Information("Client certificate added to HttpClient handler for WebApi sink");
						}
						else
						{
							Log.Warning("WebApi certificate authentication enabled but no certificate could be loaded");
						}
					}
					
					// Configure certificate authentication for AzureFunction sink if enabled
					if (azureFunctionOptions.UseCertificateAuth)
					{
						var certificate = LoadAndValidateCertificate(
							azureFunctionOptions.CertificateThumbprint,
							azureFunctionOptions.CertificatePath,
							azureFunctionOptions.CertificatePassword,
							azureFunctionOptions.CertificateStoreLocation,
							azureFunctionOptions.CertificateStoreName,
							azureFunctionOptions.ValidateCertificateChain,
							azureFunctionOptions.CheckCertificateRevocation,
							azureFunctionOptions.ExpectedCARootName,
							azureFunctionOptions.ExpectedCARootThumbprint,
							azureFunctionOptions.ExpectedSubordinateCAs,
							"AzureFunction");
						
						if (certificate != null)
						{
							handler.ClientCertificates.Add(certificate);
							Log.Information("Client certificate added to HttpClient handler for AzureFunction sink");
						}
						else
						{
							Log.Warning("AzureFunction certificate authentication enabled but no certificate could be loaded");
						}
					}
					
					return handler;
				});

			services.AddSecureBootWatcherOptions(configuration);

			// Register SinkConfigurationProvider with fallback to appsettings.json
			services.AddSingleton<SinkConfigurationProvider>(sp =>
			{
				var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SinkConfigurationProvider>();
				var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
				var options = sp.GetRequiredService<IOptionsMonitor<SecureBootWatcherOptions>>();
				var fallbackSinkOptions = options.CurrentValue.Sinks;
				
				return new SinkConfigurationProvider(logger, httpClientFactory, fallbackSinkOptions);
			});

			services.AddSingleton<IRegistrySnapshotProvider, RegistrySnapshotProvider>();
			services.AddSingleton<IEventLogReader, EventLogReader>();
			services.AddSingleton<IEventCheckpointStore, FileEventCheckpointStore>();
			services.AddSingleton<ISecureBootCertificateEnumerator, PowerShellSecureBootCertificateEnumerator>();
			
			// Register Client Update Service (needs IHttpClientFactory, so register after AddHttpClient)
			services.AddSingleton<IClientUpdateService, ClientUpdateService>();
			
			// Register Command Processor (optional - only registered if enabled in config)
			services.AddSingleton<ICommandProcessor, CommandProcessor>();
			
			services.AddSingleton<IReportBuilder, ReportBuilder>();
			services.AddSingleton<SecureBootWatcherService>();

			services.AddSingleton<FileShareReportSink>();
			services.AddSingleton<AzureQueueReportSink>();
			services.AddSingleton<WebApiReportSink>();
			services.AddSingleton<AzureFunctionReportSink>();

			// Register SinkCoordinator as the main IReportSink
			services.AddSingleton<IReportSink>(sp =>
			{
				var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SinkCoordinator>();
                var configProvider = sp.GetRequiredService<SinkConfigurationProvider>();

                // Get all sink instances
                var allSinks = new List<IReportSink>
				{
					sp.GetRequiredService<FileShareReportSink>(),
					sp.GetRequiredService<AzureQueueReportSink>(),
					sp.GetRequiredService<WebApiReportSink>(),
					sp.GetRequiredService<AzureFunctionReportSink>()
				};

                return new SinkCoordinator(logger, configProvider, allSinks);
            });

			return services.BuildServiceProvider();
		}

		private static void LogConfiguration(SecureBootWatcherOptions options)
		{
			Log.Information("========================================");
			Log.Information("Configuration:");
			Log.Information("========================================");
			
			if (!string.IsNullOrEmpty(options.FleetId))
			{
				Log.Information("Fleet ID: {FleetId}", options.FleetId);
			}
			
			Log.Information("Run Mode: {RunMode}", options.RunMode);
			Log.Information("Registry Poll Interval: {Interval}", options.RegistryPollInterval);
			Log.Information("Event Query Interval: {Interval}", options.EventQueryInterval);
			Log.Information("Event Lookback Period: {Period}", options.EventLookbackPeriod);
			
			Log.Information("Event Channels: {Count}", options.EventChannels?.Length ?? 0);
			if (options.EventChannels != null)
			{
				foreach (var channel in options.EventChannels)
				{
					Log.Information("  - {Channel}", channel);
				}
			}

			Log.Information("----------------------------------------");
			Log.Information("Sink Configuration:");
			Log.Information("  Execution Strategy: {Strategy}", options.Sinks.ExecutionStrategy);
			Log.Information("  Sink Priority: {Priority}", options.Sinks.SinkPriority);
			
			Log.Information("  File Share Sink: {Enabled}", options.Sinks.EnableFileShare ? "Enabled" : "Disabled");
			if (options.Sinks.EnableFileShare)
			{
				Log.Information("    Root Path: {Path}", options.Sinks.FileShare.RootPath ?? "NOT SET");
				Log.Information("    File Extension: {Extension}", options.Sinks.FileShare.FileExtension);
			}
			
			Log.Information("  Azure Queue Sink: {Enabled}", options.Sinks.EnableAzureQueue ? "Enabled" : "Disabled");
			if (options.Sinks.EnableAzureQueue)
			{
				Log.Information("    Queue Service URI: {Uri}", options.Sinks.AzureQueue.QueueServiceUri?.ToString() ?? "NOT SET");
				Log.Information("    Queue Name: {Name}", options.Sinks.AzureQueue.QueueName);
				Log.Information("    Authentication Method: {Method}", options.Sinks.AzureQueue.AuthenticationMethod);
				
				if (options.Sinks.AzureQueue.AuthenticationMethod.Equals("Certificate", StringComparison.OrdinalIgnoreCase))
				{
					Log.Information("    Certificate Store: {Location}\\{Store}", 
						options.Sinks.AzureQueue.CertificateStoreLocation, 
						options.Sinks.AzureQueue.CertificateStoreName);
					
					if (!string.IsNullOrEmpty(options.Sinks.AzureQueue.CertificateThumbprint))
					{
						Log.Information("    Certificate Thumbprint: {Thumbprint}", 
							options.Sinks.AzureQueue.CertificateThumbprint);
					}
				}
			}
			
			Log.Information("  Web API Sink: {Enabled}", options.Sinks.EnableWebApi ? "Enabled" : "Disabled");
			if (options.Sinks.EnableWebApi)
			{
				Log.Information("    Base Address: {Address}", options.Sinks.WebApi.BaseAddress?.ToString() ?? "NOT SET");
				Log.Information("    Ingestion Route: {Route}", options.Sinks.WebApi.IngestionRoute);
				Log.Information("    HTTP Timeout: {Timeout}", options.Sinks.WebApi.HttpTimeout);
			}

			Log.Information("  Azure Function Sink: {Enabled}", options.Sinks.EnableAzureFunction ? "Enabled" : "Disabled");
			if (options.Sinks.EnableAzureFunction)
			{
				Log.Information("    Function URL: {Url}", options.Sinks.AzureFunction.FunctionUrl?.ToString() ?? "NOT SET");
				Log.Information("    HTTP Timeout: {Timeout}", options.Sinks.AzureFunction.HttpTimeout);
				Log.Information("    API Key Configured: {KeyConfigured}", !string.IsNullOrWhiteSpace(options.Sinks.AzureFunction.ApiKey) ? "Yes" : "No");
				Log.Information("    Certificate Auth: {CertAuth}", options.Sinks.AzureFunction.UseCertificateAuth ? "Enabled" : "Disabled");
				
				if (options.Sinks.AzureFunction.UseCertificateAuth)
				{
					Log.Information("    Certificate Store: {Location}\\{Store}", 
						options.Sinks.AzureFunction.CertificateStoreLocation, 
						options.Sinks.AzureFunction.CertificateStoreName);
					
					if (!string.IsNullOrEmpty(options.Sinks.AzureFunction.CertificateThumbprint))
					{
						Log.Information("    Certificate Thumbprint: {Thumbprint}", 
							options.Sinks.AzureFunction.CertificateThumbprint);
					}
					
					Log.Information("    Validate Certificate Chain: {ValidateChain}", options.Sinks.AzureFunction.ValidateCertificateChain);
					Log.Information("    Check Certificate Revocation: {CheckRevocation}", options.Sinks.AzureFunction.CheckCertificateRevocation);
				}
			}
			
			Log.Information("----------------------------------------");
			Log.Information("Command Processing Configuration:");
			Log.Information("  Enabled: {Enabled}", options.Commands.EnableCommandProcessing ? "Enabled" : "Disabled");
			if (options.Commands.EnableCommandProcessing)
			{
				Log.Information("    Process Before Inventory: {Before}", options.Commands.ProcessBeforeInventory);
				Log.Information("    Max Commands Per Cycle: {Max}", options.Commands.MaxCommandsPerCycle);
				Log.Information("    Command Execution Delay: {Delay}", options.Commands.CommandExecutionDelay);
				Log.Information("    Continue On Command Failure: {Continue}", options.Commands.ContinueOnCommandFailure);
			}
			
			Log.Information("========================================");

			// Log active sinks
			var activeSinks = new List<string>();
			if (options.Sinks.EnableFileShare) activeSinks.Add("FileShare");
			if (options.Sinks.EnableAzureQueue) activeSinks.Add("AzureQueue");
			if (options.Sinks.EnableWebApi) activeSinks.Add("WebApi");
			if (options.Sinks.EnableAzureFunction) activeSinks.Add("AzureFunction");

			if (activeSinks.Count > 0)
			{
				Log.Information("Active Sinks: {Sinks}", string.Join(", ", activeSinks));
			}
			else
			{
				Log.Warning("⚠️ WARNING: No sinks are enabled!");
				Log.Warning("   Reports will not be sent anywhere.");
				Log.Warning("   Enable at least one sink in appsettings.json:");
				Log.Warning("   - EnableFileShare: true");
				Log.Warning("   - EnableAzureQueue: true");
				Log.Warning("   - EnableWebApi: true");
				Log.Warning("   - EnableAzureFunction: true");
			}
			
			Log.Information("========================================");
		}

		private static System.Security.Cryptography.X509Certificates.X509Certificate2? LoadAndValidateCertificate(
			string? thumbprint,
			string? path,
			string? password,
			string storeLocation,
			string storeName,
			bool validateChain,
			bool checkRevocation,
			string? expectedCARootName,
			string? expectedCARootThumbprint,
			System.Collections.Generic.List<SecureBootWatcher.Shared.Configuration.CertificateAuthorityConfig> expectedSubordinateCAs,
			string sinkName)
		{
			System.Security.Cryptography.X509Certificates.X509Certificate2? certificate = null;

			try
			{
				// Try to load from certificate store first
				if (!string.IsNullOrEmpty(thumbprint))
				{
					Log.Information("Loading client certificate for {SinkName} from store: {Thumbprint}", sinkName, thumbprint);
					
					var location = storeLocation.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase)
						? System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine
						: System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser;
					
					var store = storeName.Equals("Root", StringComparison.OrdinalIgnoreCase)
						? System.Security.Cryptography.X509Certificates.StoreName.Root
						: System.Security.Cryptography.X509Certificates.StoreName.My;
					
					using (var certStore = new System.Security.Cryptography.X509Certificates.X509Store(store, location))
					{
						certStore.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
						var certificates = certStore.Certificates.Find(
							System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,
							thumbprint,
							false);
						
						if (certificates.Count > 0)
						{
							certificate = certificates[0];
							Log.Information("Client certificate for {SinkName} loaded from store: Subject={Subject}, Issuer={Issuer}", 
								sinkName, certificate.Subject, certificate.Issuer);
						}
						else
						{
							Log.Error("Client certificate for {SinkName} not found in store with thumbprint: {Thumbprint}", 
								sinkName, thumbprint);
							return null;
						}
					}
				}
				// Otherwise try to load from file
				else if (!string.IsNullOrEmpty(path))
				{
					Log.Information("Loading client certificate for {SinkName} from file: {Path}", sinkName, path);
					
					// Note: Using old constructors for .NET Framework 4.8 compatibility
					#pragma warning disable SYSLIB0057 // X509Certificate2 constructors are obsolete
					if (!string.IsNullOrEmpty(password))
					{
						certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(path, password);
					}
					else
					{
						certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(path);
					}
					#pragma warning restore SYSLIB0057
					
					Log.Information("Client certificate for {SinkName} loaded from file: Subject={Subject}, Issuer={Issuer}", 
						sinkName, certificate.Subject, certificate.Issuer);
				}
				else
				{
					Log.Warning("No certificate path or thumbprint specified for {SinkName}", sinkName);
					return null;
				}

				if (certificate == null)
				{
					return null;
				}

				// Validate certificate validity (expiration dates)
				var now = DateTime.Now;
				if (now < certificate.NotBefore || now > certificate.NotAfter)
				{
					Log.Error("Client certificate for {SinkName} is not valid. NotBefore={NotBefore}, NotAfter={NotAfter}, Current={Now}",
						sinkName, certificate.NotBefore, certificate.NotAfter, now);
					return null;
				}

				Log.Information("Client certificate for {SinkName} validity check passed. Valid from {NotBefore} to {NotAfter}",
					sinkName, certificate.NotBefore, certificate.NotAfter);

				// Validate certificate chain if requested
				if (validateChain)
				{
					Log.Information("Validating certificate chain for {SinkName}...", sinkName);
					
					using (var chain = new System.Security.Cryptography.X509Certificates.X509Chain())
					{
						// Configure chain validation options
						chain.ChainPolicy.RevocationMode = checkRevocation
							? System.Security.Cryptography.X509Certificates.X509RevocationMode.Online
							: System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
						
						chain.ChainPolicy.RevocationFlag = System.Security.Cryptography.X509Certificates.X509RevocationFlag.EntireChain;
						chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(30);
						chain.ChainPolicy.VerificationFlags = System.Security.Cryptography.X509Certificates.X509VerificationFlags.NoFlag;

						// Build and validate the certificate chain
						bool chainIsValid = chain.Build(certificate);

						if (!chainIsValid)
						{
							Log.Warning("Certificate chain validation for {SinkName} reported issues:", sinkName);
							
							foreach (var chainStatus in chain.ChainStatus)
							{
								Log.Warning("  Chain Status: {Status} - {Information}", 
									chainStatus.Status, chainStatus.StatusInformation);
							}

							// Check if errors are critical
							bool hasCriticalError = false;
							foreach (var chainStatus in chain.ChainStatus)
							{
								// Allow some non-critical warnings
								if (chainStatus.Status != System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError &&
									chainStatus.Status != System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot)
								{
									hasCriticalError = true;
									break;
								}
							}

							if (hasCriticalError)
							{
								Log.Error("Certificate chain validation for {SinkName} failed with critical errors", sinkName);
								return null;
							}
							else
							{
								Log.Warning("Certificate chain validation for {SinkName} has warnings but no critical errors. Proceeding...", sinkName);
							}
						}
						else
						{
							Log.Information("Certificate chain validation for {SinkName} passed successfully", sinkName);
						}

						// Log chain information
						Log.Information("Certificate chain for {SinkName} has {Count} certificates:", sinkName, chain.ChainElements.Count);
						for (int i = 0; i < chain.ChainElements.Count; i++)
						{
							var element = chain.ChainElements[i];
							Log.Debug("  [{Index}] Subject={Subject}, Issuer={Issuer}", 
								i, element.Certificate.Subject, element.Certificate.Issuer);
						}

						// Validate expected CA Root if configured
						if (!string.IsNullOrWhiteSpace(expectedCARootName) || !string.IsNullOrWhiteSpace(expectedCARootThumbprint))
						{
							Log.Information("Validating expected CA Root for {SinkName}...", sinkName);
							
							// Get the root certificate from the chain (last element)
							if (chain.ChainElements.Count > 0)
							{
								var rootCert = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
								bool rootValid = true;

								if (!string.IsNullOrWhiteSpace(expectedCARootName))
								{
									if (!rootCert.Subject.Contains(expectedCARootName, StringComparison.OrdinalIgnoreCase))
									{
										Log.Error("CA Root name validation failed for {SinkName}. Expected: {Expected}, Actual: {Actual}",
											sinkName, expectedCARootName, rootCert.Subject);
										rootValid = false;
									}
									else
									{
										Log.Information("CA Root name validation passed for {SinkName}: {Subject}", sinkName, rootCert.Subject);
									}
								}

								if (!string.IsNullOrWhiteSpace(expectedCARootThumbprint))
								{
									var normalizedExpected = expectedCARootThumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();
									var normalizedActual = rootCert.Thumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();
									
									if (normalizedExpected != normalizedActual)
									{
										Log.Error("CA Root thumbprint validation failed for {SinkName}. Expected: {Expected}, Actual: {Actual}",
											sinkName, expectedCARootThumbprint, rootCert.Thumbprint);
										rootValid = false;
									}
									else
									{
										Log.Information("CA Root thumbprint validation passed for {SinkName}: {Thumbprint}", sinkName, rootCert.Thumbprint);
									}
								}

								if (!rootValid)
								{
									Log.Error("CA Root validation failed for {SinkName}", sinkName);
									return null;
								}
							}
							else
							{
								Log.Warning("No certificates in chain to validate CA Root for {SinkName}", sinkName);
							}
						}

						// Validate expected Subordinate CAs if configured
						if (expectedSubordinateCAs != null && expectedSubordinateCAs.Count > 0)
						{
							Log.Information("Validating {Count} expected Subordinate CAs for {SinkName}...", expectedSubordinateCAs.Count, sinkName);
							
							foreach (var expectedCA in expectedSubordinateCAs)
							{
								if (string.IsNullOrWhiteSpace(expectedCA.Name) && string.IsNullOrWhiteSpace(expectedCA.Thumbprint))
								{
									continue; // Skip empty configurations
								}

								bool caFound = false;
								
								// Search for the CA in the chain (excluding the leaf certificate at index 0)
								for (int i = 1; i < chain.ChainElements.Count - 1; i++)
								{
									var chainCert = chain.ChainElements[i].Certificate;
									bool nameMatches = true;
									bool thumbprintMatches = true;

									if (!string.IsNullOrWhiteSpace(expectedCA.Name))
									{
										nameMatches = chainCert.Subject.Contains(expectedCA.Name, StringComparison.OrdinalIgnoreCase);
									}

									if (!string.IsNullOrWhiteSpace(expectedCA.Thumbprint))
									{
										var normalizedExpected = expectedCA.Thumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();
										var normalizedActual = chainCert.Thumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();
										thumbprintMatches = normalizedExpected == normalizedActual;
									}

									if (nameMatches && thumbprintMatches)
									{
										caFound = true;
										Log.Information("Subordinate CA found in chain for {SinkName}: Subject={Subject}, Thumbprint={Thumbprint}",
											sinkName, chainCert.Subject, chainCert.Thumbprint);
										break;
									}
								}

								if (!caFound)
								{
									Log.Error("Expected Subordinate CA not found in certificate chain for {SinkName}. Expected Name: {Name}, Thumbprint: {Thumbprint}",
										sinkName, expectedCA.Name ?? "(not specified)", expectedCA.Thumbprint ?? "(not specified)");
									return null;
								}
							}
							
							Log.Information("All expected Subordinate CAs validated successfully for {SinkName}", sinkName);
						}
					}

					if (checkRevocation)
					{
						Log.Information("Certificate revocation check was performed for {SinkName} as part of chain validation", sinkName);
					}
				}
				else
				{
					Log.Warning("Certificate chain validation is disabled for {SinkName}. This is NOT recommended for production.", sinkName);
				}

				// Verify certificate has private key (required for client authentication)
				if (!certificate.HasPrivateKey)
				{
					Log.Error("Client certificate for {SinkName} does not have a private key. Client authentication will not work.", sinkName);
					return null;
				}

				Log.Information("Client certificate for {SinkName} has private key and is ready for use", sinkName);
				return certificate;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to load and validate client certificate for {SinkName}", sinkName);
				return null;
			}
		}
	}
}
