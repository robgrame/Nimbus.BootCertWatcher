using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Client.Configuration;
using SecureBootWatcher.Client.Services;
using SecureBootWatcher.Client.Sinks;
using SecureBootWatcher.Client.Storage;
using SecureBootWatcher.Shared.Configuration;

namespace SecureBootWatcher.Client
{
	internal static class Program
	{
		private static async Task<int> Main(string[] args)
		{
			using var cancellationSource = new CancellationTokenSource();
			Console.CancelKeyPress += (_, eventArgs) =>
			{
				eventArgs.Cancel = true;
				cancellationSource.Cancel();
			};

			var configuration = BuildConfiguration(args);
			using var serviceProvider = BuildServices(configuration);

			var bootstrapLogger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SecureBootWatcher");
			bootstrapLogger.LogInformation("Secure Boot watcher initializing.");

			var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<SecureBootWatcherOptions>>();
			LogActiveSinks(bootstrapLogger, optionsMonitor.CurrentValue);

			try
			{
				var service = serviceProvider.GetRequiredService<SecureBootWatcherService>();
				await service.RunAsync(cancellationSource.Token).ConfigureAwait(false);
				return 0;
			}
			catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
			{
				bootstrapLogger.LogInformation("Secure Boot watcher cancelled by user.");
				return 0;
			}
			catch (Exception ex)
			{
				bootstrapLogger.LogCritical(ex, "Secure Boot watcher terminated unexpectedly.");
				return 1;
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

			services.AddLogging(builder =>
			{
				builder.AddConfiguration(configuration.GetSection("Logging"));
				builder.AddConsole();
				builder.SetMinimumLevel(LogLevel.Information);
			});

			services.AddHttpClient("SecureBootIngestion");

			services.AddSecureBootWatcherOptions(configuration);

			services.AddSingleton<IRegistrySnapshotProvider, RegistrySnapshotProvider>();
			services.AddSingleton<IEventLogReader, EventLogReader>();
			services.AddSingleton<IEventCheckpointStore, FileEventCheckpointStore>();
			services.AddSingleton<IReportBuilder, ReportBuilder>();
			services.AddSingleton<SecureBootWatcherService>();

			services.AddSingleton<FileShareReportSink>();
			services.AddSingleton<AzureQueueReportSink>();
			services.AddSingleton<WebApiReportSink>();

			services.AddSingleton<IReportSink>(sp =>
			{
				var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<SecureBootWatcherOptions>>();
				var sinks = new List<IReportSink>();
				var options = optionsMonitor.CurrentValue;

				if (options.Sinks.EnableFileShare)
				{
					sinks.Add(sp.GetRequiredService<FileShareReportSink>());
				}

				if (options.Sinks.EnableAzureQueue)
				{
					sinks.Add(sp.GetRequiredService<AzureQueueReportSink>());
				}

				if (options.Sinks.EnableWebApi)
				{
					sinks.Add(sp.GetRequiredService<WebApiReportSink>());
				}

				if (sinks.Count == 0)
				{
					var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SecureBootWatcher");
					logger.LogWarning("No report sinks are enabled. Reports will be built but not forwarded.");
				}

				return new CompositeReportSink(sinks);
			});

			return services.BuildServiceProvider();
		}

		private static void LogActiveSinks(ILogger logger, SecureBootWatcherOptions options)
		{
			var targets = new List<string>();
			if (options.Sinks.EnableFileShare)
			{
				targets.Add("FileShare");
			}

			if (options.Sinks.EnableAzureQueue)
			{
				targets.Add("AzureQueue");
			}

			if (options.Sinks.EnableWebApi)
			{
				targets.Add("WebApi");
			}

			logger.LogInformation("Active Secure Boot sinks: {Targets}", targets.Count > 0 ? string.Join(", ", targets) : "None");
		}
	}
}
