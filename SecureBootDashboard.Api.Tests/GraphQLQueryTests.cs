using Microsoft.EntityFrameworkCore;
using SecureBootDashboard.Api.Data;
using SecureBootDashboard.Api.GraphQL.Queries;
using SecureBootDashboard.Api.GraphQL.Types;

namespace SecureBootDashboard.Api.Tests;

/// <summary>
/// Tests for GraphQL Query resolvers.
/// </summary>
public class GraphQLQueryTests
{
    private SecureBootDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SecureBootDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new SecureBootDbContext(options);
    }

    [Fact]
    public async Task GetDevices_ReturnsAllDevices()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var query = new Query();

        var device1 = new DeviceEntity
        {
            Id = Guid.NewGuid(),
            MachineName = "TEST-PC-01",
            DomainName = "contoso.com",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        var device2 = new DeviceEntity
        {
            Id = Guid.NewGuid(),
            MachineName = "TEST-PC-02",
            DomainName = "contoso.com",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5),
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        dbContext.Devices.AddRange(device1, device2);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await query.GetDevices(dbContext, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.MachineName == "TEST-PC-01");
        Assert.Contains(result, d => d.MachineName == "TEST-PC-02");
    }

    [Fact]
    public async Task GetDevice_WithValidId_ReturnsDevice()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var query = new Query();

        var deviceId = Guid.NewGuid();
        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TEST-PC-01",
            DomainName = "contoso.com",
            Manufacturer = "Dell",
            Model = "OptiPlex 7090",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        dbContext.Devices.Add(device);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await query.GetDevice(deviceId, dbContext, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(deviceId, result.Id);
        Assert.Equal("TEST-PC-01", result.MachineName);
        Assert.Equal("contoso.com", result.DomainName);
        Assert.Equal("Dell", result.Manufacturer);
        Assert.Equal("OptiPlex 7090", result.Model);
    }

    [Fact]
    public async Task GetDevice_WithInvalidId_ReturnsNull()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var query = new Query();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await query.GetDevice(nonExistentId, dbContext, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDeviceReports_ReturnsReportsForDevice()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var query = new Query();

        var deviceId = Guid.NewGuid();
        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TEST-PC-01",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        var report1 = new SecureBootReportEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            RegistryStateJson = "{}",
            DeploymentState = "Deployed",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-2)
        };

        var report2 = new SecureBootReportEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            RegistryStateJson = "{}",
            DeploymentState = "Pending",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
        };

        dbContext.Devices.Add(device);
        dbContext.Reports.AddRange(report1, report2);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await query.GetDeviceReports(deviceId, 10, dbContext, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        // Should be ordered by CreatedAtUtc descending
        Assert.Equal("Pending", result[0].DeploymentState);
        Assert.Equal("Deployed", result[1].DeploymentState);
    }

    [Fact]
    public async Task GetDeviceReports_RespectsLimitParameter()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var query = new Query();

        var deviceId = Guid.NewGuid();
        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TEST-PC-01",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        dbContext.Devices.Add(device);

        // Add 5 reports
        for (int i = 0; i < 5; i++)
        {
            dbContext.Reports.Add(new SecureBootReportEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                RegistryStateJson = "{}",
                CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-i)
            });
        }

        await dbContext.SaveChangesAsync();

        // Act
        var result = await query.GetDeviceReports(deviceId, 3, dbContext, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetReport_WithValidId_ReturnsReport()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var query = new Query();

        var reportId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TEST-PC-01",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        var report = new SecureBootReportEntity
        {
            Id = reportId,
            DeviceId = deviceId,
            RegistryStateJson = "{\"test\":\"data\"}",
            DeploymentState = "Deployed",
            ClientVersion = "1.0.0",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Devices.Add(device);
        dbContext.Reports.Add(report);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await query.GetReport(reportId, dbContext, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(reportId, result.Id);
        Assert.Equal(deviceId, result.DeviceId);
        Assert.Equal("Deployed", result.DeploymentState);
        Assert.Equal("1.0.0", result.ClientVersion);
    }

    [Fact]
    public async Task GetRecentReports_ReturnsRecentReports()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var query = new Query();

        var deviceId = Guid.NewGuid();
        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TEST-PC-01",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        dbContext.Devices.Add(device);

        var report1 = new SecureBootReportEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            RegistryStateJson = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-3)
        };

        var report2 = new SecureBootReportEntity
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            RegistryStateJson = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
        };

        dbContext.Reports.AddRange(report1, report2);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await query.GetRecentReports(10, dbContext, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        // Should be ordered by CreatedAtUtc descending
        Assert.True(result[0].CreatedAtUtc > result[1].CreatedAtUtc);
    }

    [Fact]
    public async Task GetReportEvents_ReturnsEventsForReport()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var query = new Query();

        var reportId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var device = new DeviceEntity
        {
            Id = deviceId,
            MachineName = "TEST-PC-01",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastSeenUtc = DateTimeOffset.UtcNow
        };

        var report = new SecureBootReportEntity
        {
            Id = reportId,
            DeviceId = deviceId,
            RegistryStateJson = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var event1 = new SecureBootEventEntity
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            ProviderName = "Microsoft-Windows-SecureBoot",
            EventId = 1001,
            TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            Level = "Information",
            Message = "Secure Boot enabled"
        };

        var event2 = new SecureBootEventEntity
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            ProviderName = "Microsoft-Windows-SecureBoot",
            EventId = 1002,
            TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Level = "Information",
            Message = "Certificate validated"
        };

        dbContext.Devices.Add(device);
        dbContext.Reports.Add(report);
        dbContext.Events.AddRange(event1, event2);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await query.GetReportEvents(reportId, dbContext, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.EventId == 1001);
        Assert.Contains(result, e => e.EventId == 1002);
    }
}
