using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.UserIdentities.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

public sealed class ContinueWithDeveloperIdentityRequestScenarioTests
{
    [Fact]
    public async Task ShouldDenyRequestGivenDisabledDeveloperAuthentication()
    {
        // Arrange
        await using var provider = BuildProvider(developerAuthentication: false);

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.Anonymous)
            // Act
            .When(new ContinueWithDeveloperIdentity("person@example.com"))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldHandleRequestGivenEnabledDeveloperAuthentication()
    {
        // Arrange
        await using var provider = BuildProvider(developerAuthentication: true);

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.Anonymous)
            // Act
            .When(new ContinueWithDeveloperIdentity("person@example.com"))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();
    }

    static ServiceProvider BuildProvider(bool developerAuthentication)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(new DeveloperUserRegistration(developerAuthentication));
        services.AddScoped<UserIdentityContinuation>();
        services.AddPortia()
            .AddRequestHandler<ContinueWithDeveloperIdentityHandler>()
            .AddRequestAuthorizer<ContinueWithDeveloperIdentityAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
