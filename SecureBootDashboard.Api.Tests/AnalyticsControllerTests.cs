using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBootDashboard.Api.Controllers;
using SecureBootDashboard.Api.Data;
using Xunit;

namespace SecureBootDashboard.Api.Tests;

public class AnalyticsControllerTests : IDisposable
{
    private readonly SecureBootDbContext _dbContext;
    private readonly AnalyticsController _controller;

    public AnalyticsControllerTests()
    {
        var options = new DbContextOptionsBuilder<SecureBootDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new SecureBootDbContext(options);
        var logger = new LoggerFactory().CreateLogger<AnalyticsController>();
        _controller = new AnalyticsController(_dbContext, logger);
    }

    [Fact]
    public async Task GetComplianceTrend_WithNoData_ReturnsEmptySnapshots()
    {
        // Act
        var result = await _controller.GetComplianceTrendAsync(7);

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(7, result.Value.Days);
        Assert.NotEmpty(result.Value.Snapshots);
        Assert.All(result.Value.Snapshots, snapshot =>
        {
            Assert.Equal(0, snapshot.TotalDevices);
            Assert.Equal(0, snapshot.DeployedDevices);
        });
    }

    [Fact]
    public async Task GetComplianceTrend_WithDeviceAndReports_ReturnsCorrectSnapshots()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TEST-PC",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            LastSeenUtc = DateTimeOffset.UtcNow
        };
        _dbContext.Devices.Add(device);

        // Add a report from 5 days ago with "Pending" state
        var report1 = new SecureBootReportEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            DeploymentState = "Pending",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5),
            RegistryStateJson = "{}"
        };
        _dbContext.Reports.Add(report1);

        // Add a report from 2 days ago with "Deployed" state
        var report2 = new SecureBootReportEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            DeploymentState = "Deployed",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            RegistryStateJson = "{}"
        };
        _dbContext.Reports.Add(report2);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetComplianceTrendAsync(7);

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(7, result.Value.Days);
        Assert.NotEmpty(result.Value.Snapshots);
        
        // Device should have existed throughout the 7-day period
        Assert.All(result.Value.Snapshots, snapshot =>
        {
            Assert.Equal(1, snapshot.TotalDevices);
        });

        // Check that deployment state changed over time
        var olderSnapshots = result.Value.Snapshots.Where(s => s.Date < DateTimeOffset.UtcNow.AddDays(-2).Date).ToList();
        var newerSnapshots = result.Value.Snapshots.Where(s => s.Date >= DateTimeOffset.UtcNow.AddDays(-2).Date).ToList();
        
        // Older snapshots should show Pending (before report2)
        Assert.All(olderSnapshots, snapshot =>
        {
            if (snapshot.Date >= DateTimeOffset.UtcNow.AddDays(-5).Date)
            {
                Assert.Equal(1, snapshot.PendingDevices);
                Assert.Equal(0, snapshot.DeployedDevices);
            }
        });

        // Newer snapshots should show Deployed (after report2)
        Assert.All(newerSnapshots, snapshot =>
        {
            Assert.Equal(1, snapshot.DeployedDevices);
            Assert.Equal(0, snapshot.PendingDevices);
            Assert.Equal(100.0, snapshot.CompliancePercentage);
        });
    }

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    public async Task GetComplianceTrend_WithValidDays_ReturnsCorrectNumberOfSnapshots(int days)
    {
        // Act
        var result = await _controller.GetComplianceTrendAsync(days);

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(days, result.Value.Days);
        Assert.Equal(days, result.Value.Snapshots.Count);
    }

    [Fact]
    public async Task GetComplianceTrend_WithInvalidDays_ReturnsBadRequest()
    {
        // Act
        var resultTooLow = await _controller.GetComplianceTrendAsync(0);
        var resultTooHigh = await _controller.GetComplianceTrendAsync(500);

        // Assert
        Assert.NotNull(resultTooLow.Result);
        Assert.NotNull(resultTooHigh.Result);
    }

    [Fact]
    public async Task GetEnrollmentTrend_WithNoDevices_ReturnsZeroCounts()
    {
        // Act
        var result = await _controller.GetEnrollmentTrendAsync(30);

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(30, result.Value.Days);
        Assert.All(result.Value.DataPoints, dp => Assert.Equal(0, dp.NewDevices));
    }

    [Fact]
    public async Task GetEnrollmentTrend_WithNewDevices_ReturnsCorrectCounts()
    {
        // Arrange
        var today = DateTimeOffset.UtcNow.Date;
        
        // Add 3 devices enrolled today
        for (int i = 0; i < 3; i++)
        {
            _dbContext.Devices.Add(new DeviceEntity
            {
                Id = Guid.NewGuid(),
                MachineName = $"TEST-PC-{i}",
                CreatedAtUtc = today.AddHours(i),
                LastSeenUtc = today.AddHours(i)
            });
        }

        // Add 2 devices enrolled 5 days ago
        for (int i = 0; i < 2; i++)
        {
            _dbContext.Devices.Add(new DeviceEntity
            {
                Id = Guid.NewGuid(),
                MachineName = $"OLD-PC-{i}",
                CreatedAtUtc = today.AddDays(-5).AddHours(i),
                LastSeenUtc = today.AddDays(-5).AddHours(i)
            });
        }

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetEnrollmentTrendAsync(7);

        // Assert
        Assert.NotNull(result.Value);
        
        var todayData = result.Value.DataPoints.FirstOrDefault(dp => dp.Date.Date == today);
        Assert.NotNull(todayData);
        Assert.Equal(3, todayData.NewDevices);

        var fiveDaysAgoData = result.Value.DataPoints.FirstOrDefault(dp => dp.Date.Date == today.AddDays(-5));
        Assert.NotNull(fiveDaysAgoData);
        Assert.Equal(2, fiveDaysAgoData.NewDevices);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
