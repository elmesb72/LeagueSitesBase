using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace LeagueSitesBackend.Tests;

public class LoginProviderTests
{
    static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();
    }

    [Fact]
    public void AllConfiguredProviders_HaveUrlGenerators()
    {
        var config = BuildConfiguration();
        var configuredProviders = config.GetSection("Authentication").GetChildren().Select(c => c.Key);

        foreach (var provider in configuredProviders)
        {
            APILoginController.SupportedProviders
                .Should().Contain(provider,
                    $"Authentication provider '{provider}' is configured in appsettings but has no URL generator in APILoginController");
        }
    }

    [Fact]
    public void AllConfiguredProviders_HaveClientId()
    {
        var config = BuildConfiguration();
        var configuredProviders = config.GetSection("Authentication").GetChildren().Select(c => c.Key);

        foreach (var provider in configuredProviders)
        {
            var clientId = config[$"Authentication:{provider}:ClientId"];
            clientId.Should().NotBeNullOrEmpty(
                $"Authentication provider '{provider}' is missing a ClientId in appsettings");
        }
    }
}
