using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SecureBootWatcher.LinuxClient.Services;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.LinuxClient.Tests.Services;

public class LinuxRegistrySnapshotProviderTests
{
    [Fact]
    public async Task CaptureAsync_ShouldReturnSnapshot()
    {
        // Arrange
        var logger = NullLogger<LinuxRegistrySnapshotProvider>.Instance;
        var provider = new LinuxRegistrySnapshotProvider(logger);
        
        // Act
        var result = await provider.CaptureAsync(CancellationToken.None);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.CollectedAtUtc <= DateTimeOffset.UtcNow);
        Assert.True(result.CollectedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task CaptureAsync_ShouldSetDeploymentState()
    {
        // Arrange
        var logger = NullLogger<LinuxRegistrySnapshotProvider>.Instance;
        var provider = new LinuxRegistrySnapshotProvider(logger);
        
        // Act
        var result = await provider.CaptureAsync(CancellationToken.None);
        
        // Assert
        Assert.NotEqual(SecureBootDeploymentState.NotStarted, result.DeploymentState);
        // Linux systems will return Unknown (no EFI vars path) or Unknown (EFI vars exist)
        // since UEFI CA 2023 tracking is Windows-specific
    }

    [Fact]
    public async Task CaptureAsync_WithCancellationToken_ShouldComplete()
    {
        // Arrange
        var logger = NullLogger<LinuxRegistrySnapshotProvider>.Instance;
        var provider = new LinuxRegistrySnapshotProvider(logger);
        var cts = new CancellationTokenSource();
        
        // Act
        var result = await provider.CaptureAsync(cts.Token);
        
        // Assert
        Assert.NotNull(result);
    }
}
