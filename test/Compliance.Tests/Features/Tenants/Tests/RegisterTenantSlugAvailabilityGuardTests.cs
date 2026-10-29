using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class RegisterTenantSlugAvailabilityGuardTests
{
    [Fact]
    public async Task ShouldAllowRegistrationGivenUnclaimedSlug()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var guard = new RegisterTenantSlugAvailabilityGuard(fixture.Repository);
        var context = Context(new RegisterTenant("Acme", "acme"));

        // Act
        var result = await guard.GuardAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectRegistrationGivenOtherTenantOwnsSlug()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var occupied = await fixture.Repository.HydrateAsync(new TenantSlug("acme"), CancellationToken.None);
        _ = occupied.Register(Uuid.CreateVersion4());
        await fixture.Repository.SaveAsync(
            occupied, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var guard = new RegisterTenantSlugAvailabilityGuard(fixture.Repository);
        var context = Context(new RegisterTenant("Acme", "acme"));

        // Act
        var result = await guard.GuardAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldAllowRetryGivenOwnedSlug()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var context = Context(new RegisterTenant("Acme", "acme"));
        var owned = await fixture.Repository.HydrateAsync(new TenantSlug("acme"), CancellationToken.None);
        _ = owned.Register(context.RequestId);
        await fixture.Repository.SaveAsync(
            owned, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var guard = new RegisterTenantSlugAvailabilityGuard(fixture.Repository);

        // Act
        var result = await guard.GuardAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldDeferValidationGivenMalformedSlug()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var guard = new RegisterTenantSlugAvailabilityGuard(fixture.Repository);
        var context = Context(new RegisterTenant("Acme", "!!"));

        // Act
        var result = await guard.GuardAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    static RequestContext<RegisterTenant> Context(RegisterTenant request) =>
        new(request, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession")));
}
