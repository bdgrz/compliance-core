using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaSeedEntryTests
{
    static readonly Uuid ClaimId = Uuid.CreateVersion4();
    [Fact]
    public void ShouldProposeOneSeedEntryAndReturnSameResultGivenAnExactRetry()
    {
        // Arrange
        var (owner, intent, request) = Seed();
        var scenario = new AggregateScenario<CriteriaEntry>(owner);

        // Act
        var first = owner.ProposeSeed(intent, 1, null, request, ClaimId);
        var retry = owner.ProposeSeed(intent, 1, null, request, ClaimId);
        var changed = owner.ProposeSeed(intent with { Summary = "Changed." }, 1, null, request, ClaimId);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        var proposed = Assert.IsType<CriterionSeedProposed>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(request.Metadata.EventId, proposed.EditionRequestEventId);
        Assert.Equal(intent, proposed.Intent);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenAChangedSeedBindingOrOperation()
    {
        // Arrange
        var (owner, intent, request) = Seed();
        var scenario = new AggregateScenario<CriteriaEntry>(owner);

        // Act
        var changedSource = owner.ProposeSeed(intent, 1, null, request with { SeedFingerprint = new string('a', 64) }, ClaimId);
        var changedOperation = owner.ProposeSeed(intent with { OperationId = Uuid.CreateVersion4() }, 1, null, request, ClaimId);

        // Assert
        Assert.False(changedSource.IsSuccess);
        Assert.False(changedOperation.IsSuccess);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldResolveTheSamePendingEntryGivenReplayedSeedProposal()
    {
        // Arrange
        var (owner, intent, request) = Seed();
        Assert.True(owner.ProposeSeed(intent, 1, null, request, ClaimId).IsSuccess);
        var proposed = Assert.IsType<CriterionSeedProposed>(Assert.Single(new AggregateScenario<CriteriaEntry>(owner).PendingEvents));
        var replayed = new CriteriaEntry(intent.EditionId, intent.Identifier, intent.OperationId);
        var scenario = new AggregateScenario<CriteriaEntry>(replayed).Given(proposed);

        // Act
        var retry = replayed.ProposeSeed(intent, 1, null, request, ClaimId);
        // Synthetic evidence exercises aggregate replay; the source verifier must qualify it.
        var evidence = new CriteriaEntryDecisionEvidence("draft", request.Metadata.EventId, 0, 1, 2);
        var outcome = replayed.Decide(intent.OperationId, 1, proposed.Metadata.EventId, true, null, evidence);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.True(outcome.IsSuccess);
        Assert.IsType<CriterionChanged>(Assert.Single(scenario.PendingEvents));
        Assert.True(replayed.Exists);
    }

    static (CriteriaEntry Owner, CriterionChangeIntent Intent, CriteriaEditionDraftRequested Request) Seed()
    {
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var item = PlatformCriteriaCatalog.EnumerateSeedEntries().First(x => x.Kind == "criterion");
        var draft = PlatformCriteriaSeedDefinition.OperationOf(edition.EditionId, "edition");
        var operation = PlatformCriteriaSeedDefinition.OperationOf(edition.EditionId, "entry:" + item.Identifier);
        var fingerprint = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());
        var request = new CriteriaEditionDraftRequested(edition.EditionId, draft, draft, 1, "tsc", edition.EditionLabel,
            new ActorAttribution("criteria-seed-bootstrap", "system"), null, CriteriaSeedFingerprint.Version, fingerprint);
        var attached = DomainEventSeed.Attach(request, new CriteriaEdition(edition.EditionId).Id, 1);
        var intent = new CriterionChangeIntent(edition.EditionId, draft, item.Identifier, operation, 0,
            "create", item.Kind, item.Category, null, null, item.Summary);
        return (new CriteriaEntry(edition.EditionId, item.Identifier, operation), intent, attached);
    }
}
