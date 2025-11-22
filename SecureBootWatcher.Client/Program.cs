using System;
using System.Collections.Generic;
using System.IO;
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
			string fileOutputTemplate;
			Serilog.Formatting.ITextFormatter textFormatter = null;
		
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
						outputTemplate: fileOutputTemplate);
				}
				else
				{
					loggerConfig.WriteTo.File(
						path: logPath,
						rollingInterval: rollingInterval,
						retainedFileCountLimit: retainedFileCountLimit,
						outputTemplate: fileOutputTemplate);
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
					var plusIndex = informationalVersion.IndexOf('+');
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

			services.AddHttpClient("SecureBootIngestion");

			services.AddSecureBootWatcherOptions(configuration);

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

			// Register SinkCoordinator as the main IReportSink
			services.AddSingleton<IReportSink>(sp =>
			{
				var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SinkCoordinator>();
				var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<SecureBootWatcherOptions>>();
				
				// Get all sink instances
				var allSinks = new List<IReportSink>
				{
					sp.GetRequiredService<FileShareReportSink>(),
					sp.GetRequiredService<AzureQueueReportSink>(),
					sp.GetRequiredService<WebApiReportSink>()
				};

				return new SinkCoordinator(logger, optionsMonitor, allSinks);
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
			}
			
			Log.Information("========================================");
		}
	}
}
