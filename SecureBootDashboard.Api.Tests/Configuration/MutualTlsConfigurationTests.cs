using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureBootDashboard.Api.Configuration;
using Xunit;

namespace SecureBootDashboard.Api.Tests.Configuration;

public class MutualTlsConfigurationTests
{
    [Fact]
    public void MutualTlsOptions_DefaultConfiguration_IsDisabled()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<MutualTlsOptions>(configuration.GetSection("MutualTls"));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<MutualTlsOptions>>().Value;

        // Assert
        Assert.False(options.Enabled);
        Assert.False(options.AllowSelfSignedCertificates);
        Assert.True(options.CheckCertificateRevocation);
        Assert.True(options.ValidateCertificateChain);
        Assert.Empty(options.AllowedThumbprints);
        Assert.Empty(options.AllowedIssuers);
    }

    [Fact]
    public void MutualTlsOptions_EnabledConfiguration_LoadsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MutualTls:Enabled"] = "true",
                ["MutualTls:AllowSelfSignedCertificates"] = "true",
                ["MutualTls:CheckCertificateRevocation"] = "false",
                ["MutualTls:ValidateCertificateChain"] = "true",
                ["MutualTls:AllowedThumbprints:0"] = "ABC123",
                ["MutualTls:AllowedThumbprints:1"] = "DEF456",
                ["MutualTls:AllowedIssuers:0"] = "Test CA",
                ["MutualTls:AllowedIssuers:1"] = "Production CA"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<MutualTlsOptions>(configuration.GetSection("MutualTls"));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<MutualTlsOptions>>().Value;

        // Assert
        Assert.True(options.Enabled);
        Assert.True(options.AllowSelfSignedCertificates);
        Assert.False(options.CheckCertificateRevocation);
        Assert.True(options.ValidateCertificateChain);
        Assert.Equal(2, options.AllowedThumbprints.Count);
        Assert.Contains("ABC123", options.AllowedThumbprints);
        Assert.Contains("DEF456", options.AllowedThumbprints);
        Assert.Equal(2, options.AllowedIssuers.Count);
        Assert.Contains("Test CA", options.AllowedIssuers);
        Assert.Contains("Production CA", options.AllowedIssuers);
    }

    [Fact]
    public void MutualTlsOptions_ProductionConfiguration_HasSecureDefaults()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MutualTls:Enabled"] = "true",
                ["MutualTls:AllowedIssuers:0"] = "Contoso Enterprise CA"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<MutualTlsOptions>(configuration.GetSection("MutualTls"));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<MutualTlsOptions>>().Value;

        // Assert - Production-safe defaults
        Assert.True(options.Enabled);
        Assert.False(options.AllowSelfSignedCertificates); // Default is false (secure)
        Assert.True(options.CheckCertificateRevocation); // Default is true (secure)
        Assert.True(options.ValidateCertificateChain); // Default is true (secure)
        Assert.Single(options.AllowedIssuers);
    }

    [Fact]
    public void MutualTlsOptions_EmptyAllowLists_AllowsAnyCertificate()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MutualTls:Enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<MutualTlsOptions>(configuration.GetSection("MutualTls"));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<MutualTlsOptions>>().Value;

        // Assert - Empty allow lists means any valid certificate is accepted
        Assert.Empty(options.AllowedThumbprints);
        Assert.Empty(options.AllowedIssuers);
    }

    [Fact]
    public void MutualTlsOptions_TestConfiguration_AllowsSelfSigned()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MutualTls:Enabled"] = "true",
                ["MutualTls:AllowSelfSignedCertificates"] = "true",
                ["MutualTls:CheckCertificateRevocation"] = "false",
                ["MutualTls:AllowedIssuers:0"] = "Test Root CA"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<MutualTlsOptions>(configuration.GetSection("MutualTls"));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<MutualTlsOptions>>().Value;

        // Assert - Test/Development configuration
        Assert.True(options.Enabled);
        Assert.True(options.AllowSelfSignedCertificates); // Allow for testing
        Assert.False(options.CheckCertificateRevocation); // Disabled for testing
        Assert.True(options.ValidateCertificateChain); // Still validate chain
    }
}
