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
using SecureBootWatcher.LinuxClient.Configuration;
using SecureBootWatcher.LinuxClient.Services;
using SecureBootWatcher.LinuxClient.Sinks;
using SecureBootWatcher.LinuxClient.Storage;
using SecureBootWatcher.Shared.Configuration;
using Serilog;
using Serilog.Events;

namespace SecureBootWatcher.LinuxClient
{
	internal static class Program
	{
		private static async Task<int> Main(string[] args)
		{
			// Configure Serilog before anything else
			var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "client-.log");
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Information()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
				.MinimumLevel.Override("System", LogEventLevel.Warning)
				.Enrich.FromLogContext()
				.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
				.WriteTo.File(
					path: logPath,
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: 30,
					outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
				.CreateLogger();

			using var cancellationSource = new CancellationTokenSource();
			
			try
			{
				// Log startup information
				Log.Information("========================================");
				Log.Information("SecureBootWatcher Linux Client Starting");
				Log.Information("========================================");
				
				// Get version info
				var assembly = Assembly.GetExecutingAssembly();
				var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
							  ?? assembly.GetName().Version?.ToString() 
							  ?? "Unknown";
				
				Log.Information("Version: {Version}", version);
				Log.Information("Base Directory: {BaseDirectory}", AppContext.BaseDirectory);
				Log.Information("Current Directory: {CurrentDirectory}", Environment.CurrentDirectory);
				Log.Information("Machine Name: {MachineName}", Environment.MachineName);
				Log.Information("User: {User}", Environment.UserName);
				Log.Information(".NET Runtime: {Framework}", Environment.Version);
				Log.Information("OS: {OS}", Environment.OSVersion);
				Log.Information("Log Path: {LogPath}", Path.GetFullPath(logPath));

				Console.CancelKeyPress += (_, eventArgs) =>
				{
					eventArgs.Cancel = true;
					Log.Information("Cancellation requested (Ctrl+C)...");
					cancellationSource.Cancel();
				};

				var configuration = BuildConfiguration(args);
				
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
				Log.Information("SecureBootWatcher Linux Client Stopped Successfully");
				Log.Information("========================================");
				return 0;
			}
			catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
			{
				Log.Information("SecureBootWatcher Linux Client cancelled by user");
				return 0;
			}
			catch (Exception ex)
			{
				Log.Fatal(ex, "SecureBootWatcher Linux Client terminated unexpectedly");
				return 1;
			}
			finally
			{
				Log.Information("Shutting down...");
				Log.CloseAndFlush();
			}
		}

		private static IConfiguration BuildConfiguration(string[] args) =>
			new ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
				.AddEnvironmentVariables(prefix: "SECUREBOOT_")
				.AddCommandLine(args)
				.Build();

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

			services.AddSingleton<IRegistrySnapshotProvider, LinuxRegistrySnapshotProvider>();
			services.AddSingleton<IEventLogReader, LinuxEventLogReader>();
			services.AddSingleton<IEventCheckpointStore, FileEventCheckpointStore>();
			services.AddSingleton<ISecureBootCertificateEnumerator, LinuxSecureBootCertificateEnumerator>();
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
