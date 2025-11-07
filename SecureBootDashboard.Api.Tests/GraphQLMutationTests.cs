using Microsoft.Extensions.Logging;
using Moq;
using SecureBootDashboard.Api.GraphQL.Mutations;
using SecureBootWatcher.Shared.Models;
using SecureBootWatcher.Shared.Storage;

namespace SecureBootDashboard.Api.Tests;

/// <summary>
/// Tests for GraphQL Mutation resolvers.
/// </summary>
public class GraphQLMutationTests
{
    [Fact]
    public async Task IngestReport_WithValidReport_ReturnsSuccess()
    {
        // Arrange
        var mockReportStore = new Mock<IReportStore>();
        var mockLogger = new Mock<ILogger<Mutation>>();
        var mutation = new Mutation();

        var reportId = Guid.NewGuid();
        mockReportStore
            .Setup(x => x.SaveAsync(It.IsAny<SecureBootStatusReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportId);

        var validReport = new SecureBootStatusReport
        {
            Device = new DeviceIdentity
            {
                MachineName = "TEST-PC-01",
                DomainName = "contoso.com",
                Manufacturer = "Dell",
                Model = "OptiPlex 7090"
            },
            Registry = new SecureBootRegistrySnapshot
            {
                CollectedAtUtc = DateTimeOffset.UtcNow,
                DeploymentState = SecureBootDeploymentState.Updated
            },
            Events = new List<SecureBootEventRecord>(),
            ClientVersion = "1.0.0",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var input = new IngestReportInput { Report = validReport };

        // Act
        var result = await mutation.IngestReport(input, mockReportStore.Object, mockLogger.Object, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(reportId, result.ReportId);
        Assert.Null(result.Errors);
        mockReportStore.Verify(x => x.SaveAsync(validReport, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestReport_WithNullReport_ReturnsError()
    {
        // Arrange
        var mockReportStore = new Mock<IReportStore>();
        var mockLogger = new Mock<ILogger<Mutation>>();
        var mutation = new Mutation();

        var input = new IngestReportInput { Report = null };

        // Act
        var result = await mutation.IngestReport(input, mockReportStore.Object, mockLogger.Object, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.ReportId);
        Assert.NotNull(result.Errors);
        Assert.Contains("Report payload is null.", result.Errors);
        mockReportStore.Verify(x => x.SaveAsync(It.IsAny<SecureBootStatusReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestReport_WithInvalidReport_ReturnsValidationErrors()
    {
        // Arrange
        var mockReportStore = new Mock<IReportStore>();
        var mockLogger = new Mock<ILogger<Mutation>>();
        var mutation = new Mutation();

        // Create an invalid report (missing required MachineName)
        var invalidReport = new SecureBootStatusReport
        {
            Device = new DeviceIdentity
            {
                MachineName = "", // Invalid: required field
                DomainName = "contoso.com"
            },
            Registry = new SecureBootRegistrySnapshot
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            },
            Events = new List<SecureBootEventRecord>(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var input = new IngestReportInput { Report = invalidReport };

        // Act
        var result = await mutation.IngestReport(input, mockReportStore.Object, mockLogger.Object, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.ReportId);
        Assert.NotNull(result.Errors);
        Assert.NotEmpty(result.Errors);
        mockReportStore.Verify(x => x.SaveAsync(It.IsAny<SecureBootStatusReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestReport_WhenStorageThrows_ReturnsError()
    {
        // Arrange
        var mockReportStore = new Mock<IReportStore>();
        var mockLogger = new Mock<ILogger<Mutation>>();
        var mutation = new Mutation();

        mockReportStore
            .Setup(x => x.SaveAsync(It.IsAny<SecureBootStatusReport>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage failure"));

        var validReport = new SecureBootStatusReport
        {
            Device = new DeviceIdentity
            {
                MachineName = "TEST-PC-01",
                DomainName = "contoso.com"
            },
            Registry = new SecureBootRegistrySnapshot
            {
                CollectedAtUtc = DateTimeOffset.UtcNow
            },
            Events = new List<SecureBootEventRecord>(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var input = new IngestReportInput { Report = validReport };

        // Act
        var result = await mutation.IngestReport(input, mockReportStore.Object, mockLogger.Object, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.ReportId);
        Assert.NotNull(result.Errors);
        Assert.Contains("Failed to persist report.", result.Errors);
    }
}
