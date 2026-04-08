using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace LeagueSitesBase.Tests;

public class ScopePolicyProviderTests
{
    static ScopePolicyProvider CreateProvider() =>
        new(Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task ScopePrefix_ParsesSingleScope()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("Scope:Executive");

        policy.Should().NotBeNull();
        policy!.Requirements.OfType<PermissionsScopeRequirement>().Should().ContainSingle()
            .Which.Scopes.Should().BeEquivalentTo([PermissionsScope.Executive]);
    }

    [Fact]
    public async Task ScopePrefix_ParsesMultipleScopes()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("Scope:Executive,Webmaster");

        policy.Should().NotBeNull();
        var requirement = policy!.Requirements.OfType<PermissionsScopeRequirement>().Single();
        requirement.Scopes.Should().BeEquivalentTo([PermissionsScope.Executive, PermissionsScope.Webmaster]);
    }

    [Fact]
    public async Task ScopePrefix_RequiresAuthentication()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("Scope:Manager");

        policy.Should().NotBeNull();
        policy!.AuthenticationSchemes.Should().BeEmpty(); // uses default
        policy.Requirements.Should().Contain(r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task NonScopePolicy_FallsBackToDefault()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("SomeOtherPolicy");

        policy.Should().BeNull("unregistered policies return null from default provider");
    }

    [Fact]
    public async Task ScopePrefix_HandlesWhitespace()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("Scope: Executive , Webmaster ");

        policy.Should().NotBeNull();
        var requirement = policy!.Requirements.OfType<PermissionsScopeRequirement>().Single();
        requirement.Scopes.Should().BeEquivalentTo([PermissionsScope.Executive, PermissionsScope.Webmaster]);
    }

    [Fact]
    public void ScopePrefix_InvalidScope_Throws()
    {
        var provider = CreateProvider();

        var act = async () => await provider.GetPolicyAsync("Scope:InvalidRole");

        act.Should().ThrowAsync<ArgumentException>();
    }
}
