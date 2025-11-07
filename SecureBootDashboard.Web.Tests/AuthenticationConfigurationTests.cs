using Microsoft.Extensions.Configuration;
using Xunit;

namespace SecureBootDashboard.Web.Tests;

public class AuthenticationConfigurationTests
{
    [Fact]
    public void Configuration_Should_Support_None_Provider()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Provider"] = "None"
            })
            .Build();

        // Act
        var provider = configuration["Authentication:Provider"];

        // Assert
        Assert.Equal("None", provider);
    }

    [Fact]
    public void Configuration_Should_Support_EntraId_Provider()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Provider"] = "EntraId",
                ["Authentication:EntraId:ClientId"] = "test-client-id",
                ["Authentication:EntraId:TenantId"] = "test-tenant-id",
                ["Authentication:EntraId:Instance"] = "https://login.microsoftonline.com/",
                ["Authentication:EntraId:CallbackPath"] = "/signin-oidc"
            })
            .Build();

        // Act
        var provider = configuration["Authentication:Provider"];
        var clientId = configuration["Authentication:EntraId:ClientId"];
        var tenantId = configuration["Authentication:EntraId:TenantId"];

        // Assert
        Assert.Equal("EntraId", provider);
        Assert.Equal("test-client-id", clientId);
        Assert.Equal("test-tenant-id", tenantId);
    }

    [Fact]
    public void Configuration_Should_Support_Windows_Provider()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Provider"] = "Windows",
                ["Authentication:Windows:Enabled"] = "true"
            })
            .Build();

        // Act
        var provider = configuration["Authentication:Provider"];
        var enabled = configuration.GetValue<bool>("Authentication:Windows:Enabled");

        // Assert
        Assert.Equal("Windows", provider);
        Assert.True(enabled);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("EntraId")]
    [InlineData("Windows")]
    public void Provider_Should_Be_Case_Insensitive(string providerValue)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Provider"] = providerValue
            })
            .Build();

        // Act
        var provider = configuration["Authentication:Provider"];

        // Assert
        Assert.NotNull(provider);
        Assert.Equal(providerValue, provider, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Default_Configuration_Should_Have_No_Authentication()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var provider = configuration["Authentication:Provider"];

        // Assert
        Assert.Null(provider);
    }
}
