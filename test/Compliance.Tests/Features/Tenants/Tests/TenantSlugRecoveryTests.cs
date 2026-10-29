using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantSlugRecoveryTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldConfirmTheTenantGivenARegisteredSlug()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.Open(OwnerId, "Acme", "acme"));

        // Act
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);

        // Assert
        var tenant = await host.TenantAsync(TenantId);
        Assert.True(tenant.IsActive);
        Assert.Equal("acme", tenant.CurrentSlug);
        Assert.Single((await host.FactsAsync("bdgrz", "tenant-slugs")).OfType<TenantSlugRegistered>());
    }

    [Fact]
    public async Task ShouldAdvanceTheCheckpointWithoutDuplicateEffectsGivenAReplayedSlugFact()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.Open(OwnerId, "Acme", "acme"));
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);
        var tenantFacts = (await host.FactsAsync("bdgrz", "tenants")).Count;
        var slugFacts = (await host.FactsAsync("bdgrz", "tenant-slugs")).Count;
        await host.RewindAsync(host.Registration);
        await host.RewindAsync(host.Slugs);

        // Act
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);
        var registrationCheckpoint = await host.CheckpointAsync(host.Registration);
        var slugCheckpoint = await host.CheckpointAsync(host.Slugs);
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);

        // Assert
        Assert.Equal(tenantFacts, (await host.FactsAsync("bdgrz", "tenants")).Count);
        Assert.Equal(slugFacts, (await host.FactsAsync("bdgrz", "tenant-slugs")).Count);
        Assert.NotEqual(ProjectionCheckpoint.Start, registrationCheckpoint);
        Assert.NotEqual(ProjectionCheckpoint.Start, slugCheckpoint);
        Assert.Equal(registrationCheckpoint, await host.CheckpointAsync(host.Registration));
        Assert.Equal(slugCheckpoint, await host.CheckpointAsync(host.Slugs));
        Assert.True((await host.TenantAsync(TenantId)).IsActive);
    }

    [Fact]
    public async Task ShouldCompleteTheChangeAndSurrenderTheOldSlugGivenAConfirmedChange()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        await SettleAsync(host, "acme");

        // Act
        await ChangeAsync(host, "acme-next");

        // Assert
        var tenant = await host.TenantAsync(TenantId);
        Assert.True(tenant.IsActive);
        Assert.Equal("acme-next", tenant.CurrentSlug);
        var slugFacts = await host.FactsAsync("bdgrz", "tenant-slugs");
        Assert.Single(slugFacts.OfType<TenantSlugSurrendered>(), surrendered => surrendered.Slug == "acme");
        Assert.Single((await host.FactsAsync("bdgrz", "tenants")).OfType<TenantSlugChanged>());
    }

    [Fact]
    public async Task ShouldAdvanceTheCheckpointGivenLateAnswersAfterANewerChangeIsPending()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        await SettleAsync(host, "acme");
        await ChangeAsync(host, "acme-next");
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.RequestSlugChange("acme-newest"));
        var tenantFacts = (await host.FactsAsync("bdgrz", "tenants")).Count;
        var slugFacts = (await host.FactsAsync("bdgrz", "tenant-slugs")).Count;
        await host.RewindAsync(host.Slugs);

        // Act
        await host.RunAsync(host.Slugs);

        // Assert
        var tenant = await host.TenantAsync(TenantId);
        Assert.Equal("acme-next", tenant.CurrentSlug);
        Assert.Equal(tenantFacts, (await host.FactsAsync("bdgrz", "tenants")).Count);
        Assert.Equal(slugFacts, (await host.FactsAsync("bdgrz", "tenant-slugs")).Count);
        Assert.NotEqual(ProjectionCheckpoint.Start, await host.CheckpointAsync(host.Slugs));
    }

    [Fact]
    public async Task ShouldCompleteTheNewerChangeGivenReplayedAnswersOfAnOlderOne()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        await SettleAsync(host, "acme");
        await ChangeAsync(host, "acme-next");
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.RequestSlugChange("acme-newest"));
        await host.RewindAsync(host.Slugs);
        await host.RewindAsync(host.Registration);

        // Act
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);

        // Assert
        var tenant = await host.TenantAsync(TenantId);
        Assert.Equal("acme-newest", tenant.CurrentSlug);
        Assert.True(tenant.IsActive);
        Assert.Equal(2, (await host.FactsAsync("bdgrz", "tenants")).OfType<TenantSlugChanged>().Count());
    }

    [Fact]
    public async Task ShouldKeepTheTenantUnchangedGivenAClaimAnotherTenantAlreadyHolds()
    {
        // Arrange
        await using var host = new TenantSlugHost();
        await SettleAsync(host, "acme");
        var other = Uuid.CreateVersion4();
        await host.SeedAsync(new Tenant(other), tenant => tenant.Open(OwnerId, "Other", "other-co"));
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);
        await host.SeedAsync(new Tenant(other), tenant => tenant.RequestSlugChange("acme"));

        // Act
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);

        // Assert
        var tenant = await host.TenantAsync(other);
        Assert.Equal("other-co", tenant.CurrentSlug);
        Assert.Single((await host.FactsAsync("bdgrz", "tenants")).OfType<TenantSlugChangeRejected>());
        await host.RewindAsync(host.Slugs);
        await host.RunAsync(host.Slugs);
        Assert.Single((await host.FactsAsync("bdgrz", "tenants")).OfType<TenantSlugChangeRejected>());
    }

    static async Task SettleAsync(TenantSlugHost host, string slug)
    {
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.Open(OwnerId, "Acme", slug));
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);
    }

    static async Task ChangeAsync(TenantSlugHost host, string slug)
    {
        await host.SeedAsync(new Tenant(TenantId), tenant => tenant.RequestSlugChange(slug));
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);
        await host.RunAsync(host.Registration);
        await host.RunAsync(host.Slugs);
    }
}
