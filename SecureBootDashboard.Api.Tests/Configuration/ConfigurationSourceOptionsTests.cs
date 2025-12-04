using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureBootDashboard.Api.Configuration;
using Xunit;

namespace SecureBootDashboard.Api.Tests.Configuration;

public class ConfigurationSourceOptionsTests
{
    [Fact]
    public void ConfigurationSourceOptions_DefaultConfiguration_UsesAppSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<ConfigurationSourceOptions>(configuration.GetSection(ConfigurationSourceOptions.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<ConfigurationSourceOptions>>().Value;

        // Assert
        Assert.Equal("AppSettings", options.Provider);
        Assert.True(options.UseAppSettingsConfiguration);
        Assert.False(options.UseDatabaseConfiguration);
    }

    [Fact]
    public void ConfigurationSourceOptions_AppSettingsProvider_LoadsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigurationSource:Provider"] = "AppSettings"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<ConfigurationSourceOptions>(configuration.GetSection(ConfigurationSourceOptions.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<ConfigurationSourceOptions>>().Value;

        // Assert
        Assert.Equal("AppSettings", options.Provider);
        Assert.True(options.UseAppSettingsConfiguration);
        Assert.False(options.UseDatabaseConfiguration);
    }

    [Fact]
    public void ConfigurationSourceOptions_DatabaseProvider_LoadsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigurationSource:Provider"] = "Database"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<ConfigurationSourceOptions>(configuration.GetSection(ConfigurationSourceOptions.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<ConfigurationSourceOptions>>().Value;

        // Assert
        Assert.Equal("Database", options.Provider);
        Assert.True(options.UseDatabaseConfiguration);
        Assert.False(options.UseAppSettingsConfiguration);
    }

    [Fact]
    public void ConfigurationSourceOptions_CaseInsensitive_AppSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigurationSource:Provider"] = "appsettings"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<ConfigurationSourceOptions>(configuration.GetSection(ConfigurationSourceOptions.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<ConfigurationSourceOptions>>().Value;

        // Assert
        Assert.Equal("appsettings", options.Provider);
        Assert.True(options.UseAppSettingsConfiguration);
        Assert.False(options.UseDatabaseConfiguration);
    }

    [Fact]
    public void ConfigurationSourceOptions_CaseInsensitive_Database()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigurationSource:Provider"] = "database"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<ConfigurationSourceOptions>(configuration.GetSection(ConfigurationSourceOptions.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<ConfigurationSourceOptions>>().Value;

        // Assert
        Assert.Equal("database", options.Provider);
        Assert.True(options.UseDatabaseConfiguration);
        Assert.False(options.UseAppSettingsConfiguration);
    }
}
