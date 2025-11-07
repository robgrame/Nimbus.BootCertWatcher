using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.Services;
using SecureBootWatcher.Shared.Models;
using Xunit;

namespace SecureBootDashboard.Api.Tests;

public class AnomalyDetectionServiceTests
{
    private SecureBootDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SecureBootDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new SecureBootDbContext(options);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_WithNoData_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var logger = new Mock<ILogger<AnomalyDetectionService>>();
        var service = new AnomalyDetectionService(context, logger.Object);

        // Act
        var result = await service.DetectAnomaliesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_WithInactiveDevice_DetectsInactivityAnomaly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var deviceId = Guid.NewGuid();
        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TestDevice",
            LastSeenUtc = DateTimeOffset.UtcNow.AddDays(-20),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-30)
        };

        var report = new SecureBootReportEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-20),
            RegistryStateJson = "{}"
        };

        context.Devices.Add(device);
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        var logger = new Mock<ILogger<AnomalyDetectionService>>();
        var service = new AnomalyDetectionService(context, logger.Object);

        // Act
        var result = await service.DetectAnomaliesAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, a => a.Type == AnomalyType.DeviceBehavior);
        Assert.Contains(result, a => a.DeviceId == deviceId);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_WithDeviceStuckInPending_DetectsDeploymentAnomaly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var deviceId = Guid.NewGuid();
        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TestDevice",
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-30)
        };

        // Add 4 reports with Pending state
        for (int i = 0; i < 4; i++)
        {
            context.Reports.Add(new SecureBootReportEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-i),
                RegistryStateJson = "{}",
                DeploymentState = "Pending"
            });
        }

        context.Devices.Add(device);
        await context.SaveChangesAsync();

        var logger = new Mock<ILogger<AnomalyDetectionService>>();
        var service = new AnomalyDetectionService(context, logger.Object);

        // Act
        var result = await service.DetectAnomaliesAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, a => a.Type == AnomalyType.DeploymentState);
        Assert.Contains(result, a => a.Description.Contains("Pending"));
    }

    [Fact]
    public async Task GetActiveAnomaliesAsync_ReturnsOnlyActiveAnomalies()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var deviceId = Guid.NewGuid();
        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TestDevice",
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        context.Devices.Add(device);

        var activeAnomaly = new AnomalyEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            AnomalyType = "DeviceBehavior",
            Description = "Test anomaly",
            Severity = 0.5,
            DetectedAtUtc = DateTimeOffset.UtcNow,
            Status = "Active"
        };

        var resolvedAnomaly = new AnomalyEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            AnomalyType = "DeviceBehavior",
            Description = "Resolved anomaly",
            Severity = 0.5,
            DetectedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            Status = "Resolved",
            ResolvedBy = "Admin",
            ResolvedAtUtc = DateTimeOffset.UtcNow
        };

        context.Anomalies.Add(activeAnomaly);
        context.Anomalies.Add(resolvedAnomaly);
        await context.SaveChangesAsync();

        var logger = new Mock<ILogger<AnomalyDetectionService>>();
        var service = new AnomalyDetectionService(context, logger.Object);

        // Act
        var result = await service.GetActiveAnomaliesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(activeAnomaly.Id, result[0].Id);
        Assert.Equal(AnomalyStatus.Active, result[0].Status);
    }

    [Fact]
    public async Task ResolveAnomalyAsync_UpdatesAnomalyStatus()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var deviceId = Guid.NewGuid();
        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TestDevice",
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        context.Devices.Add(device);

        var anomaly = new AnomalyEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            AnomalyType = "DeviceBehavior",
            Description = "Test anomaly",
            Severity = 0.5,
            DetectedAtUtc = DateTimeOffset.UtcNow,
            Status = "Active"
        };

        context.Anomalies.Add(anomaly);
        await context.SaveChangesAsync();

        var logger = new Mock<ILogger<AnomalyDetectionService>>();
        var service = new AnomalyDetectionService(context, logger.Object);

        // Act
        await service.ResolveAnomalyAsync(anomaly.Id, "TestUser");

        // Assert
        var updatedAnomaly = await context.Anomalies.FindAsync(anomaly.Id);
        Assert.NotNull(updatedAnomaly);
        Assert.Equal("Resolved", updatedAnomaly.Status);
        Assert.Equal("TestUser", updatedAnomaly.ResolvedBy);
        Assert.NotNull(updatedAnomaly.ResolvedAtUtc);
    }
}
