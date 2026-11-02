using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Rules;
namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;

/// <summary>Aggregate-stage tests for the InformationAsset owner: one classification and retention record per identity.</summary>
public sealed class InformationAssetTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRaiseNormalizedActiveRevisionOneGivenNewAsset()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var asset = new InformationAsset(TenantId, id);

        // Act
        var result = asset.Record(Content(" Customer PII ", " restricted ", " RET-1 ") with
        {
            Description = "  ",
            Lifecycle = TechnologyInventoryRules.Retired,
        }, Author, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value!.InformationAssetId);
        var recorded = Assert.IsType<InformationAssetRevisionRecorded>(Assert.Single(
            new AggregateScenario<InformationAsset>(asset).PendingEvents));
        Assert.Equal(TenantId, recorded.TenantId);
        Assert.Equal(id, recorded.InformationAssetId);
        Assert.Equal(1, recorded.Revision);
        Assert.Equal("Customer PII", recorded.Content.Name);
        Assert.Equal("restricted", recorded.Content.Classification);
        Assert.Equal("RET-1", recorded.Content.RetentionReference);
        Assert.Null(recorded.Content.Description);
        Assert.Equal(TechnologyInventoryRules.Active, recorded.Content.Lifecycle);
        Assert.True(asset.IsCreated);
        Assert.Equal(1, asset.Revision);
    }

    [Fact]
    public void ShouldRefuseCreationAndAppendNothingMoreGivenRecordedAsset()
    {
        // Arrange
        var asset = new InformationAsset(TenantId, Uuid.CreateVersion4());
        Assert.True(asset.Record(Content("Customer PII", "restricted", "RET-1"), Author, Now).IsSuccess);

        // Act
        var same = asset.Record(Content("Customer PII", "restricted", "RET-1"), Author, Now);
        var changed = asset.Record(Content("Customer PII", "public", "RET-1"), Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, same.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Single(new AggregateScenario<InformationAsset>(asset).PendingEvents);
        Assert.Equal(1, asset.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenInvalidCreationContent()
    {
        // Arrange
        var asset = new InformationAsset(TenantId, Uuid.CreateVersion4());
        var invalid = new[]
        {
            Content(" ", "internal", "RET-1"),
            Content(new string('n', 201), "internal", "RET-1"),
            Content("Asset", "internal", new string('r', 201)),
            Content("Asset", "internal", "RET-1") with { OwnerPersonId = Uuid.Empty },
            Content("Asset", "internal", "RET-1") with { Description = new string('d', 2001) },
        };

        // Act
        var results = invalid.Select(content => asset.Record(content, Author, Now)).ToArray();

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind));
        Assert.Empty(new AggregateScenario<InformationAsset>(asset).PendingEvents);
        Assert.False(asset.IsCreated);
    }

    [Fact]
    public void ShouldAcceptBoundaryLengthsGivenMaximumNameRetentionAndDescription()
    {
        // Arrange
        var asset = new InformationAsset(TenantId, Uuid.CreateVersion4());
        var content = Content(new string('n', 200), "public", new string('r', 200)) with
        {
            Description = new string('d', 2000),
        };

        // Act
        var result = asset.Record(content, Author, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, asset.Content!.Name.Length);
    }

    [Fact]
    public void ShouldRefuseCreationGivenOperationOfAnotherOwnerOrWithoutLogicalId()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var asset = new InformationAsset(TenantId, id);
        var fact = InventoryFacts.AssetFact(TenantId, id, 1, Content("Asset", "internal", "RET-1"), Author, Now);
        var otherOwner = new InformationAsset(TenantId, Uuid.CreateVersion4()).Stream;

        // Act
        var foreignOwner = asset.Record(fact.Operation with { Owner = otherOwner }, fact.FirstResponse!,
            Content("Asset", "internal", "RET-1"), Author, Now);
        var noLogicalId = asset.Record(fact.Operation with { LogicalId = Uuid.Empty }, fact.FirstResponse!,
            Content("Asset", "internal", "RET-1"), Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, foreignOwner.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, noLogicalId.Error!.Kind);
        Assert.Empty(new AggregateScenario<InformationAsset>(asset).PendingEvents);
    }

    [Fact]
    public void ShouldAdvanceRevisionAndRetireGivenRevisionAtCurrentRevision()
    {
        // Arrange
        var asset = new InformationAsset(TenantId, Uuid.CreateVersion4());
        Assert.True(asset.Record(Content("Customer PII", "restricted", "RET-1"), Author, Now).IsSuccess);

        // Act
        var reclassified = asset.Revise(1, current => current with { Classification = " confidential " }, Author,
            Now);
        var retired = asset.Revise(2, current => current with { Lifecycle = TechnologyInventoryRules.Retired },
            Author, Now);

        // Assert
        Assert.True(reclassified.IsSuccess);
        Assert.True(retired.IsSuccess);
        Assert.Equal(3, asset.Revision);
        Assert.Equal("confidential", asset.Content!.Classification);
        Assert.Equal(TechnologyInventoryRules.Retired, asset.Content.Lifecycle);
        var revisions = new AggregateScenario<InformationAsset>(asset).PendingEvents
            .OfType<InformationAssetRevisionRecorded>().Select(fact => fact.Revision).ToArray();
        Assert.Equal(new long[] { 1, 2, 3 }, revisions);
    }

    [Fact]
    public void ShouldRefuseSecondRevisionGivenCompetingRevisionsAtSameExpectedRevision()
    {
        // Arrange
        var asset = new InformationAsset(TenantId, Uuid.CreateVersion4());
        Assert.True(asset.Record(Content("Customer PII", "restricted", "RET-1"), Author, Now).IsSuccess);

        // Act
        var winner = asset.Revise(1, current => current with { Name = "Winner" }, Author, Now);
        var loser = asset.Revise(1, current => current with { Name = "Loser" }, Author, Now);

        // Assert
        Assert.True(winner.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, loser.Error!.Kind);
        Assert.Equal("Winner", asset.Content!.Name);
        Assert.Equal(2, asset.Revision);
        Assert.Equal(2, new AggregateScenario<InformationAsset>(asset).PendingEvents.Count);
    }

    [Fact]
    public void ShouldAppendNothingGivenInvalidRevisionOrMissingAsset()
    {
        // Arrange
        var missing = new InformationAsset(TenantId, Uuid.CreateVersion4());
        var asset = new InformationAsset(TenantId, Uuid.CreateVersion4());
        Assert.True(asset.Record(Content("Customer PII", "restricted", "RET-1"), Author, Now).IsSuccess);

        // Act
        var absent = missing.Revise(1, current => current, Author, Now);
        var badClassification = asset.Revise(1, current => current with { Classification = "secret" }, Author, Now);
        var twoClassifications = asset.Revise(1, current => current with { Classification = "public,internal" },
            Author, Now);
        var blankRetention = asset.Revise(1, current => current with { RetentionReference = " " }, Author, Now);
        var badLifecycle = asset.Revise(1, current => current with { Lifecycle = "archived" }, Author, Now);
        var noOwner = asset.Revise(1, current => current with { OwnerPersonId = Uuid.Empty }, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, absent.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, badClassification.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, twoClassifications.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, blankRetention.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, badLifecycle.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, noOwner.Error!.Kind);
        Assert.Empty(new AggregateScenario<InformationAsset>(missing).PendingEvents);
        Assert.Single(new AggregateScenario<InformationAsset>(asset).PendingEvents);
        Assert.Equal(1, asset.Revision);
    }

    [Fact]
    public void ShouldRefuseRevisionGivenOperationOfAnotherOwnerOrWithoutLogicalId()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var asset = new InformationAsset(TenantId, id);
        Assert.True(asset.Record(Content("Customer PII", "restricted", "RET-1"), Author, Now).IsSuccess);
        var fact = InventoryFacts.AssetFact(TenantId, id, 2, Content("Customer PII", "public", "RET-1"), Author, Now);
        var otherOwner = new InformationAsset(TenantId, Uuid.CreateVersion4()).Stream;

        // Act
        var foreignOwner = asset.Revise(fact.Operation with { Owner = otherOwner }, fact.FirstResponse!, 1,
            current => current with { Classification = "public" }, Author, Now);
        var noLogicalId = asset.Revise(fact.Operation with { LogicalId = Uuid.Empty }, fact.FirstResponse!, 1,
            current => current with { Classification = "public" }, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, foreignOwner.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, noLogicalId.Error!.Kind);
        Assert.Equal("restricted", asset.Content!.Classification);
        Assert.Single(new AggregateScenario<InformationAsset>(asset).PendingEvents);
    }

    [Fact]
    public void ShouldRestoreRevisionAndContentGivenValidHistoryReplay()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var created = InventoryFacts.AssetFact(TenantId, id, 1, Content("Customer PII", "restricted", "RET-1"),
            Author, Now);
        var revised = InventoryFacts.AssetFact(TenantId, id, 2,
            Content("Customer PII", "confidential", "RET-2") with { Lifecycle = TechnologyInventoryRules.Retired },
            Author, Now.AddHours(1));
        DomainEvent[] history =
        [
            DomainEventSeed.Attach(created, id, 1),
            DomainEventSeed.Attach(revised, id, 2),
        ];

        // Act
        var asset = new AggregateScenario<InformationAsset>(new InformationAsset(TenantId, id)).Given(history)
            .Aggregate;
        var next = asset.Revise(2, current => current with { Lifecycle = TechnologyInventoryRules.Active }, Author,
            Now.AddHours(2));
        var stale = asset.Revise(1, current => current with { Name = "Stale" }, Author, Now.AddHours(2));

        // Assert
        Assert.True(asset.IsCreated);
        Assert.Equal(3, asset.Revision);
        Assert.True(next.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal("RET-2", asset.Content!.RetentionReference);
        Assert.Equal("confidential", asset.Content.Classification);
    }

    [Fact]
    public void ShouldDecideReplayedGivenSelectedOperationAfterLaterRevision()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var created = InventoryFacts.AssetFact(TenantId, id, 1, Content("Customer PII", "restricted", "RET-1"),
            Author, Now);
        var revised = InventoryFacts.AssetFact(TenantId, id, 2, Content("Customer PII", "public", "RET-1"), Author,
            Now.AddHours(1));
        DomainEvent[] history =
        [
            DomainEventSeed.Attach(created, id, 1),
            DomainEventSeed.Attach(revised, id, 2),
        ];
        var binding = AcceptedOperationBinding.Of(created.Operation);

        // Act
        var hydrated = new AggregateScenario<InformationAsset>(
            new InformationAsset(TenantId, id, created.Operation.LogicalId)).Given(history).Aggregate;
        var conflicting = AcceptedOperationBinding.Of(created.Operation with
        {
            AuthoredIntentSha256 = new string('c', 64),
        });

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, hydrated.Acceptance.Decide(binding));
        Assert.Equal(NativeAcceptanceDecision.Conflict, hydrated.Acceptance.Decide(conflicting));
        Assert.Equal(2, hydrated.Revision);
    }

    [Fact]
    public void ShouldDecideNoneGivenUnacceptedLogicalOperation()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var created = InventoryFacts.AssetFact(TenantId, id, 1, Content("Customer PII", "restricted", "RET-1"),
            Author, Now);
        var other = created.Operation with { LogicalId = Uuid.CreateVersion4() };
        DomainEvent[] history = [DomainEventSeed.Attach(created, id, 1)];

        // Act
        var hydrated = new AggregateScenario<InformationAsset>(new InformationAsset(TenantId, id, other.LogicalId))
            .Given(history).Aggregate;

        // Assert
        Assert.Equal(NativeAcceptanceDecision.None, hydrated.Acceptance.Decide(AcceptedOperationBinding.Of(other)));
    }

    [Fact]
    public void ShouldNotReviseGivenForeignInventoryStream()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var foreign = InventoryFacts.ComponentFact(TenantId, id, 1,
            new TechnologyComponentContent("network", "VPC", OwnerId, "production", "us-east-1", null, null, null,
                "active"), Author, Now);
        DomainEvent[] history = [DomainEventSeed.Attach(foreign, id, 1)];

        // Act
        var asset = new AggregateScenario<InformationAsset>(new InformationAsset(TenantId, id)).Given(history)
            .Aggregate;
        var revise = asset.Revise(1, current => current, Author, Now);

        // Assert
        Assert.False(asset.IsCreated);
        Assert.Null(asset.Content);
        Assert.Equal(RequestErrorKind.NotFound, revise.Error!.Kind);
    }

    static InformationAssetContent Content(string name, string classification, string retention) =>
        new(name, classification, retention, OwnerId, null, TechnologyInventoryRules.Active);
}
