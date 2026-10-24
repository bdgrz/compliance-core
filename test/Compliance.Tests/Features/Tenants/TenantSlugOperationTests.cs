using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantSlugOperationTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid OtherTenantId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReplayWithoutAppendingGivenAnExactRetryOfAClaim()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        var claim = new RegisterTenantSlug(Uuid.CreateVersion4(), TenantId, "acme");
        Assert.True((await DispatchAsync(host, claim)).IsSuccess);

        // Act
        var retry = await DispatchAsync(host, claim);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Single(await host.FactsAsync("bdgrz", "tenant-slugs"));
    }

    [Fact]
    public async Task ShouldConflictGivenTheSameClaimIdentityForAnotherTenant()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        var commandId = Uuid.CreateVersion4();
        Assert.True((await DispatchAsync(host, new RegisterTenantSlug(commandId, TenantId, "acme"))).IsSuccess);

        // Act
        var changed = await DispatchAsync(host, new RegisterTenantSlug(commandId, OtherTenantId, "acme"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Single(await host.FactsAsync("bdgrz", "tenant-slugs"));
    }

    [Fact]
    public async Task ShouldAppendNothingGivenASecondClaimByTheHoldingTenant()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        Assert.True((await DispatchAsync(host, new RegisterTenantSlug(Uuid.CreateVersion4(), TenantId, "acme"))).IsSuccess);

        // Act
        var second = await DispatchAsync(host, new RegisterTenantSlug(Uuid.CreateVersion4(), TenantId, "acme"));

        // Assert
        Assert.True(second.IsSuccess);
        Assert.Single(await host.FactsAsync("bdgrz", "tenant-slugs"));
    }

    [Fact]
    public async Task ShouldAnswerEachRefusedClaimOnceGivenRetriesFromAnotherTenant()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        Assert.True((await DispatchAsync(host, new RegisterTenantSlug(Uuid.CreateVersion4(), TenantId, "acme"))).IsSuccess);
        var refused = new RegisterTenantSlug(Uuid.CreateVersion4(), OtherTenantId, "acme");

        // Act
        Assert.True((await DispatchAsync(host, refused)).IsSuccess);
        Assert.True((await DispatchAsync(host, refused)).IsSuccess);
        Assert.True((await DispatchAsync(host, refused)).IsSuccess);

        // Assert
        var facts = await host.FactsAsync("bdgrz", "tenant-slugs");
        Assert.Single(facts.OfType<TenantSlugRegistered>());
        Assert.Single(facts.OfType<TenantSlugRegistrationRejected>());
    }

    [Fact]
    public async Task ShouldAppendNothingGivenASurrenderOfAnAlreadySurrenderedSlug()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        Assert.True((await DispatchAsync(host, new RegisterTenantSlug(Uuid.CreateVersion4(), TenantId, "acme"))).IsSuccess);
        Assert.True((await DispatchAsync(host, new SurrenderTenantSlug(Uuid.CreateVersion4(), TenantId, "acme"))).IsSuccess);

        // Act
        var again = await DispatchAsync(host, new SurrenderTenantSlug(Uuid.CreateVersion4(), TenantId, "acme"));

        // Assert
        Assert.True(again.IsSuccess);
        Assert.Single((await host.FactsAsync("bdgrz", "tenant-slugs")).OfType<TenantSlugSurrendered>());
    }

    [Fact]
    public async Task ShouldReplayGivenAnExactRetryOfAConfirmationAfterTheTenantMovedOn()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.Open(OwnerId, "Acme", "acme"));
        var confirm = new ConfirmTenantSlug(Uuid.CreateVersion4(), TenantId, "acme");
        Assert.True((await DispatchAsync(host, confirm)).IsSuccess);
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.RequestSlugChange("acme-next"));
        var before = (await host.FactsAsync("bdgrz", "tenants")).Count;

        // Act
        var retry = await DispatchAsync(host, confirm);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(before, (await host.FactsAsync("bdgrz", "tenants")).Count);
    }

    [Fact]
    public async Task ShouldConflictGivenTheSameConfirmationIdentityWithAnotherSlug()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.Open(OwnerId, "Acme", "acme"));
        var commandId = Uuid.CreateVersion4();
        Assert.True((await DispatchAsync(host, new ConfirmTenantSlug(commandId, TenantId, "acme"))).IsSuccess);
        var before = (await host.FactsAsync("bdgrz", "tenants")).Count;

        // Act
        var changed = await DispatchAsync(host, new ConfirmTenantSlug(commandId, TenantId, "acme-other"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(before, (await host.FactsAsync("bdgrz", "tenants")).Count);
    }

    [Fact]
    public async Task ShouldSucceedWithoutAppendingGivenALateRefusalAfterTheTenantConfirmed()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.Open(OwnerId, "Acme", "acme"));
        Assert.True((await DispatchAsync(host, new ConfirmTenantSlug(Uuid.CreateVersion4(), TenantId, "acme"))).IsSuccess);
        var before = (await host.FactsAsync("bdgrz", "tenants")).Count;

        // Act
        var late = await DispatchAsync(host, new RejectTenantSlug(Uuid.CreateVersion4(), TenantId, "acme"));

        // Assert
        Assert.True(late.IsSuccess);
        Assert.Equal(before, (await host.FactsAsync("bdgrz", "tenants")).Count);
        Assert.True((await host.TenantAsync(TenantId)).IsActive);
    }

    [Fact]
    public async Task ShouldSucceedWithoutAppendingGivenAnAnswerForAnUnknownTenant()
    {
        // Arrange
        await using var host = new TenantSlugHost();

        // Act
        var confirmed = await DispatchAsync(host, new ConfirmTenantSlug(Uuid.CreateVersion4(), TenantId, "acme"));
        var released = await DispatchAsync(host,
            new ConfirmTenantSlugSurrender(Uuid.CreateVersion4(), TenantId, "acme"));
        var refused = await DispatchAsync(host,
            new RejectTenantSlugSurrender(Uuid.CreateVersion4(), TenantId, "acme"));

        // Assert
        Assert.True(confirmed.IsSuccess);
        Assert.True(released.IsSuccess);
        Assert.True(refused.IsSuccess);
        Assert.Empty(await host.FactsAsync("bdgrz", "tenants"));
    }

    [Fact]
    public async Task ShouldRejectTheClaimGivenAReservedRoute()
    {
        // Arrange
        await using var host = new TenantSlugHost();

        // Act
        var claim = await DispatchAsync(host, new RegisterTenantSlug(Uuid.CreateVersion4(), TenantId, "login"));

        // Assert
        Assert.True(claim.IsSuccess);
        Assert.Single((await host.FactsAsync("bdgrz", "tenant-slugs")).OfType<TenantSlugRegistrationRejected>());
    }

    static ValueTask<Result> DispatchAsync(TenantSlugHost host, IRequest request) => host.Bus.DispatchAsync(request,
        new RequestDispatchContext(RequestActor.System), CancellationToken.None);
}
