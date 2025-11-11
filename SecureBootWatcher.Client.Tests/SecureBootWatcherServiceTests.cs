using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureBootWatcher.Client.Services;
using SecureBootWatcher.Client.Sinks;
using SecureBootWatcher.Shared.Configuration;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Tests;

[TestClass]
public sealed class SecureBootWatcherServiceTests
{
    [TestMethod]
    public async Task RunAsync_WithRunModeOnce_ExecutesOnlyOnce()
    {
        // Arrange
        var executionCount = 0;
        var mockReportBuilder = new MockReportBuilder(() => executionCount++);
        var mockSink = new MockReportSink();
        var options = new SecureBootWatcherOptions
        {
            RunMode = "Once",
            RegistryPollInterval = TimeSpan.FromSeconds(1),
            EventQueryInterval = TimeSpan.FromSeconds(1)
        };
        var optionsMonitor = new MockOptionsMonitor(options);
        var logger = new MockLogger();

        var service = new SecureBootWatcherService(
            logger,
            mockReportBuilder,
            mockSink,
            optionsMonitor);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.RunAsync(cts.Token);

        // Assert
        Assert.AreEqual(1, executionCount, "Service should execute exactly once in 'Once' mode");
        Assert.AreEqual(1, mockSink.EmitCount, "Sink should receive exactly one report");
    }

    [TestMethod]
    public async Task RunAsync_WithRunModeContinuous_ExecutesMultipleTimes()
    {
        // Arrange
        var executionCount = 0;
        var mockReportBuilder = new MockReportBuilder(() => executionCount++);
        var mockSink = new MockReportSink();
        var options = new SecureBootWatcherOptions
        {
            RunMode = "Continuous",
            RegistryPollInterval = TimeSpan.FromMilliseconds(100),
            EventQueryInterval = TimeSpan.FromMilliseconds(100)
        };
        var optionsMonitor = new MockOptionsMonitor(options);
        var logger = new MockLogger();

        var service = new SecureBootWatcherService(
            logger,
            mockReportBuilder,
            mockSink,
            optionsMonitor);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await service.RunAsync(cts.Token);

        // Assert
        Assert.IsTrue(executionCount > 1, $"Service should execute multiple times in 'Continuous' mode, but executed {executionCount} times");
    }

    // Mock implementations
    private class MockReportBuilder : IReportBuilder
    {
        private readonly Action _onBuild;

        public MockReportBuilder(Action onBuild)
        {
            _onBuild = onBuild;
        }

        public Task<SecureBootStatusReport> BuildAsync(CancellationToken cancellationToken)
        {
            _onBuild();
            return Task.FromResult(new SecureBootStatusReport
            {
                Device = new DeviceIdentity
                {
                    MachineName = "TestDevice",
                    DomainName = "TestDomain"
                },
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    private class MockReportSink : IReportSink
    {
        public int EmitCount { get; private set; }

        public Task EmitAsync(SecureBootStatusReport report, CancellationToken cancellationToken)
        {
            EmitCount++;
            return Task.CompletedTask;
        }
    }

    private class MockOptionsMonitor : IOptionsMonitor<SecureBootWatcherOptions>
    {
        public MockOptionsMonitor(SecureBootWatcherOptions options)
        {
            CurrentValue = options;
        }

        public SecureBootWatcherOptions CurrentValue { get; }

        public SecureBootWatcherOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SecureBootWatcherOptions, string?> listener) => null;
    }

    private class MockLogger : ILogger<SecureBootWatcherService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // No-op for testing
        }
    }
}
