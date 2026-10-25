using System.Text;
using Cntryl.Fitz;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class IndependenceCatalogNativeReadTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task ShouldClassifyExactHistoricalCatalogFailureGivenVerifiedCurrentHead(bool ratification, bool unavailable)
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var (staff, duty, actor, author) = IndependenceCatalogNativeBoundaryTests.Authority();
        var rules = new IndependenceRuleCatalogRules();
        var decisions = new IndependenceRuleRatificationRules();
        for (var index = 1; index <= 2; index++)
        {
            var value = new IndependenceRuleContent(12, [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")], "Source " + index);
            Assert.True(rules.ReviseRules(Uuid.CreateVersion4(), rules.Sequence, value, author, DateTimeOffset.UtcNow).IsSuccess);
            Assert.True(decisions.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), index, index, decisions.Sequence,
                IndependenceSourceDigest.RuleContent(value), "Decision " + index, staff, duty, actor, DateTimeOffset.UtcNow).IsSuccess);
        }
        await fixture.SeedAsync(rules);
        await fixture.SeedAsync(decisions);
        var selected = ratification
            ? Assert.IsType<IndependenceRuleRatificationRecorded>((await fixture.FactsAsync(new IndependenceRuleRatificationCatalog().Stream))[0]).RatificationContent
            : Assert.IsType<IndependenceRulesRevised>((await fixture.FactsAsync(new IndependenceRuleCatalog().Stream))[0]).RuleContent;
        var faulting = new FaultingContent(fixture.Content, selected, unavailable);
        var handler = new GetIndependenceRuleVersionsHandler(new(fixture.Reader, faulting), new(fixture.Reader, faulting));

        // Act
        var result = await handler.HandleAsync(IndependenceCatalogSourceFixture.Context(new GetIndependenceRuleVersions(), Uuid.CreateVersion4()), CancellationToken.None);
        var currentRules = await fixture.Rules.ReadAsync();
        var currentDecision = await fixture.Ratifications.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Equal(2, currentRules.Sequence);
        Assert.Equal(2, currentDecision.Sequence);
        Assert.Equal(2, (await fixture.Rules.ReadVersionsAsync()).Count);
        Assert.Equal(2, (await fixture.Ratifications.ReadHistoryAsync()).Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReplayOriginalBeforeCurrentCatalogReadGivenAcceptedRevisionAndLaterRuleBodyFailure(bool unavailable)
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var user = Uuid.CreateVersion4();
        var value = new IndependenceRuleContent(12, [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")], "First source");
        var first = new ReviseIndependenceRules(Uuid.CreateVersion4(), 0, value);
        var handler = new ReviseIndependenceRulesHandler(fixture.Operations());
        var original = await handler.HandleAsync(IndependenceCatalogSourceFixture.Context(first, user), CancellationToken.None);
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True((await handler.HandleAsync(IndependenceCatalogSourceFixture.Context(new ReviseIndependenceRules(Uuid.CreateVersion4(), 1,
            value with { SourceReference = "Later source" }), user), CancellationToken.None)).IsSuccess);
        var current = await fixture.Rules.ReadAsync();
        var failing = new ReviseIndependenceRulesHandler(fixture.Operations(new FaultingContent(fixture.Content, current.Owner.CurrentContent!, unavailable)));

        // Act
        var replay = await failing.HandleAsync(IndependenceCatalogSourceFixture.Context(first, user, "Changed profile"), CancellationToken.None);
        var fresh = await failing.HandleAsync(IndependenceCatalogSourceFixture.Context(first with { CommandId = Uuid.CreateVersion4(), ExpectedSequence = 2 }, user), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(original.Value.Version, replay.Value.Version);
        Assert.Equal(original.Value.RecordedAt, replay.Value.RecordedAt);
        Assert.Equal(original.Value.Actor, replay.Value.Actor);
        Assert.Equal(RequestErrorKind.Conflict, fresh.Error?.Kind);
        Assert.Equal(unavailable, fresh.Error!.IsTransient);
        Assert.Equal(2, (await fixture.FactsAsync(current.Owner.Stream)).Count);
    }

    [Fact]
    public async Task ShouldRefuseMixedActiveRatificationEvidenceGivenNewerDecisionBeforeSourcePin()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await PersonalDecisionFacts.SeedPartnerAsync(host);
        var reader = host.Scope.ServiceProvider.GetRequiredService<CurrentRatifiedIndependenceRulesReader>();
        var original = await reader.ReadActiveAsync(CancellationToken.None);
        Assert.NotNull(original);
        var revised = partner.Rules with { SourceReference = "Later classified rules" };
        await host.SeedDraftRulesAsync(revised);
        await ProgramManagementServices.SeedAsync(host.Provider, new IndependenceRuleRatificationRules(), rules =>
            rules.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, 2, rules.Sequence, IndependenceSourceDigest.RuleContent(revised),
                "Later personal ratification", partner.Ratifier, partner.RatifierDuty,
                ActorReference.ForFirmStaff(partner.Ratifier.UserId, "Ratifier"), partner.At));
        var pins = host.Scope.ServiceProvider.GetRequiredService<EngagementSourcePins>();

        // Act
        var stale = await pins.RulesAsync(original, CancellationToken.None);
        var current = await reader.ReadActiveAsync(CancellationToken.None);
        var currentPins = await pins.RulesAsync(current!, CancellationToken.None);

        // Assert
        Assert.Null(stale);
        Assert.NotNull(current);
        Assert.Equal(2, current.Version);
        Assert.NotNull(currentPins);
        Assert.Equal(2, currentPins.Count);
        var owner = await host.HydrateAsync(new IndependenceRuleRatificationCatalog());
        var ratificationPin = Assert.Single(currentPins, value => value.SourceOwner.Area == IndependenceRuleRatificationCatalog.Area);
        Assert.Equal(owner.LatestRatification!.Metadata.EventId, ratificationPin.ContentEvent.EventId);
        Assert.Equal(owner.LatestRatification.Metadata.AggregateVersion, ratificationPin.ContentEvent.AggregateVersion);
        Assert.Equal((ulong)owner.Sequence, ratificationPin.ResourceRevision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyDamagedRatificationAuthorityBeforePersonalAdmissionAndAcceptanceGivenActualStoredBody(bool unavailable)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await PersonalDecisionFacts.SeedPartnerAsync(host);
        var (engagement, _) = await PersonalDecisionFacts.SeedEngagementAsync(host);
        var request = await PersonalDecisionFacts.EvaluationAsync(host, engagement, partner);
        var source = await host.Scope.ServiceProvider.GetRequiredService<IndependenceRuleRatificationReader>().ReadAsync();
        await DamageAsync(host.Scope.ServiceProvider.GetRequiredService<IKvClient>(), source.Owner.CurrentContent!, unavailable);

        // Act
        var admission = await host.HttpAsync(request, partner.Actor);
        var evidence = await host.Scope.ServiceProvider.GetRequiredService<IServiceEngagementAcceptanceEvidenceReader>()
            .ReadCurrentAsync(host.Tenant, engagement, partner.Staff.UserId, CancellationToken.None);
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, admission.Error?.Kind);
        Assert.Equal(unavailable, admission.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, evidence.Error?.Kind);
        Assert.Equal(unavailable, evidence.Error!.IsTransient);
        Assert.Null(owner.DecisionOf(request.EvaluationId));
    }

    static async Task DamageAsync(IKvClient client, ImmutableContentRef reference, bool unavailable)
    {
        var hex = reference.ContentId.ToString().Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        await using var transaction = await client.BeginAsync($"kv://bdgrz/immutable-content/{IndependenceCatalogContent.Realm}-{hex[..2]}", KvMode.ReadWrite);
        if (unavailable)
            await transaction.DeleteAsync(Encoding.UTF8.GetBytes($"m/{hex}"));
        else
        {
            var key = Encoding.UTF8.GetBytes($"c/{hex}/000000");
            var stored = await transaction.GetAsync(key);
            Assert.True(stored.Found);
            var bytes = stored.Value!.Value.ToArray();
            bytes[0] ^= 1;
            await transaction.PutAsync(key, bytes);
        }
        await transaction.CommitAsync(KvDurability.Sync);
    }

    sealed class FaultingContent(IImmutableContentStore inner, ImmutableContentRef selected, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            reference == selected ? Task.FromException<ReadOnlyMemory<byte>>(unavailable ? new ImmutableContentUnavailableException("Missing exact catalog body.") :
                new ImmutableContentIntegrityException("Corrupt exact catalog body.")) : inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
