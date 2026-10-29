using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Projections;
namespace Bdgrz.Compliance.Tests.Features.Retention.Tests;

/// <summary>
///     Owner-level tests of the artifact legal hold: one placement by its own root operation, one exact release of the
///     placed content, refusals that append nothing, exact retry, and replay of valid history.
/// </summary>
public sealed class ArtifactLegalHoldTests
{
    static readonly DateTimeOffset PlacedAt = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ReleasedAt = new(2027, 1, 7, 0, 0, 0, TimeSpan.Zero);

    static ArtifactLegalHold Open(Uuid tenantId, Uuid holdId, Uuid selectedLogicalId) =>
        new(tenantId, holdId, selectedLogicalId);

    static ArtifactRetentionSourceSnapshot Snapshot(Uuid tenantId, ulong position = 5) =>
        new(new ArtifactRetentionSource(tenantId, "application_import", Uuid.CreateVersion4(), new string('a', 64)),
            position);

    static OperationProvenance PlaceOperation(ArtifactLegalHold owner, long? expectedRevision = 0,
        DateTimeOffset? at = null) =>
        NativeFacts.Root(owner, owner.Id, "PlaceArtifactLegalHold", expectedRevision, null, at ?? PlacedAt);

    static OperationProvenance ReleaseOperation(ArtifactLegalHold owner, Uuid commandId, long? expectedRevision = 1,
        DateTimeOffset? at = null) =>
        NativeFacts.Root(owner, commandId, "ReleaseArtifactLegalHold", expectedRevision, null, at ?? ReleasedAt);

    static Result Place(ArtifactLegalHold owner, OperationProvenance operation, ArtifactRetentionSourceSnapshot source,
        string reason = "Legal matter") =>
        owner.Place(operation, source, reason, NativeFacts.Ref(), NativeFacts.Ref());

    static Result Release(ArtifactLegalHold owner, OperationProvenance operation,
        ArtifactRetentionSourceSnapshot source, string reason = "Matter closed") =>
        owner.ReleaseHold(operation, source, reason, NativeFacts.Ref(), NativeFacts.Ref());

    static int Pending(ArtifactLegalHold owner) =>
        new AggregateScenario<ArtifactLegalHold>(owner).PendingEvents.Count;

    static ArtifactLegalHoldPlaced PlacedFact(ArtifactLegalHold owner) =>
        Assert.IsType<ArtifactLegalHoldPlaced>(Assert.Single(new AggregateScenario<ArtifactLegalHold>(owner).PendingEvents));

    static ArtifactLegalHoldReleased ReleasedFact(ArtifactLegalHold owner) =>
        Assert.IsType<ArtifactLegalHoldReleased>(Assert.Single(new AggregateScenario<ArtifactLegalHold>(owner).PendingEvents));

    static DomainEvent[] PlacedHistory(ArtifactLegalHold placedSeed) => NativeFacts.Raised(placedSeed);

    static ArtifactLegalHold PlacedHold(Uuid tenantId, Uuid holdId, Uuid selectedLogicalId,
        ArtifactRetentionSourceSnapshot source, out OperationProvenance placement)
    {
        var seed = Open(tenantId, holdId, holdId);
        placement = PlaceOperation(seed);
        Assert.True(Place(seed, placement, source).IsSuccess);
        return NativeFacts.Replay(Open(tenantId, holdId, selectedLogicalId), PlacedHistory(seed));
    }

    static ArtifactLegalHold ReleasedHold(Uuid tenantId, Uuid holdId, Uuid commandId, Uuid selectedLogicalId,
        ArtifactRetentionSourceSnapshot source, out OperationProvenance placement, out OperationProvenance release)
    {
        var seed = Open(tenantId, holdId, holdId);
        placement = PlaceOperation(seed);
        Assert.True(Place(seed, placement, source).IsSuccess);
        var placedFacts = PlacedHistory(seed);
        var reopened = NativeFacts.Replay(Open(tenantId, holdId, commandId), placedFacts);
        release = ReleaseOperation(reopened, commandId);
        Assert.True(Release(reopened, release, source).IsSuccess);
        var json = Bdgrz.Compliance.ComplianceCoreJsonContext.Default.ArtifactLegalHoldReleased;
        var copy = System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(ReleasedFact(reopened), json), json)!;
        var history = new[] { placedFacts[0], DomainEventSeed.Attach(copy, holdId, 2UL) };
        return NativeFacts.Replay(Open(tenantId, holdId, selectedLogicalId), history);
    }

    [Fact]
    public void ShouldKeepStreamOfHoldGivenTenantAndHoldIdsOnly()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();

        // Act
        var first = Open(tenantId, holdId, Uuid.CreateVersion4());
        var second = new ArtifactLegalHold(tenantId, holdId);

        // Assert
        var expected = new EventStreamAddress(tenantId.ToString(), ArtifactLegalHold.Area, holdId.ToString());
        Assert.Equal(expected, first.Stream);
        Assert.Equal(expected, second.Stream);
        Assert.Equal(0, first.Revision);
        Assert.Null(first.Placement);
        Assert.Null(first.View);
    }

    [Fact]
    public void ShouldPlaceRevisionOneHoldGivenValidRootOperation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = Open(tenantId, holdId, holdId);
        var operation = PlaceOperation(owner);

        // Act
        var result = Place(owner, operation, source, "  Legal matter  ");

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var placed = PlacedFact(owner);
        Assert.Equal(source.Source, placed.Source);
        Assert.Equal(source.Position, placed.SourcePosition);
        Assert.Equal(holdId, placed.HoldId);
        Assert.Equal("Legal matter", placed.Reason);
        Assert.Equal(1, placed.Revision);
        Assert.Equal(operation.Actor, placed.RecordedBy);
        Assert.Equal(PlacedAt, placed.RecordedAt);
        Assert.Equal(holdId, placed.Operation.LogicalId);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(source.Source, owner.Source);
        Assert.Null(owner.Release);
    }

    [Fact]
    public void ShouldAppendNothingGivenPlacementNotRootedInHoldOrMissingBusinessFacts()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var owner = Open(tenantId, holdId, holdId);
        var elsewhere = Open(tenantId, Uuid.CreateVersion4(), Uuid.Empty);
        var source = Snapshot(tenantId);
        var otherTenant = Snapshot(Uuid.CreateVersion4());
        var unsupported = source with { Source = source.Source with { SourceKind = "unknown_kind" } };
        var badDigest = source with { Source = source.Source with { ContentSha256 = new string('A', 64) } };

        // Act
        var otherLogical = Place(owner,
            NativeFacts.Root(owner, Uuid.CreateVersion4(), "PlaceArtifactLegalHold", 0), source);
        var otherStream = Place(owner, NativeFacts.Root(elsewhere, owner.Id, "PlaceArtifactLegalHold", 0), source);
        var otherKind = Place(owner, NativeFacts.Root(owner, owner.Id, "ReleaseArtifactLegalHold", 0), source);
        var withoutRevision = Place(owner, PlaceOperation(owner, null), source);
        var nonZero = Place(owner, PlaceOperation(owner, 3), source);
        var noReason = Place(owner, PlaceOperation(owner), source, "   ");
        var longReason = Place(owner, PlaceOperation(owner), source, new string('x', 2001));
        var foreignTenant = Place(owner, PlaceOperation(owner), otherTenant);
        var noPosition = Place(owner, PlaceOperation(owner), source with { Position = 0 });
        var unsupportedKind = Place(owner, PlaceOperation(owner), unsupported);
        var malformedDigest = Place(owner, PlaceOperation(owner), badDigest);

        // Assert
        Assert.All(new[]
        {
            otherLogical, otherStream, otherKind, withoutRevision, nonZero, noReason, longReason, foreignTenant,
            noPosition, unsupportedKind, malformedDigest,
        }, result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        });
        Assert.Equal(0, Pending(owner));
        Assert.Null(owner.Placement);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseNullResponseContentGivenPlacementAndAppendNothing()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var owner = Open(tenantId, holdId, holdId);
        var source = Snapshot(tenantId);
        var operation = PlaceOperation(owner);
        var reference = NativeFacts.Ref();

        // Act
        var noFirst = Record.Exception(() =>
        {
            _ = owner.Place(operation, source, "Legal matter", null!, reference);
        });
        var noResult = Record.Exception(() =>
        {
            _ = owner.Place(operation, source, "Legal matter", reference, null!);
        });

        // Assert
        Assert.IsType<ArgumentNullException>(noFirst);
        Assert.IsType<ArgumentNullException>(noResult);
        Assert.Equal(0, Pending(owner));
        Assert.Null(owner.Placement);
    }

    [Fact]
    public void ShouldRefuseNullResponseContentGivenReleaseAndAppendNothing()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = PlacedHold(tenantId, holdId, commandId, source, out _);
        var operation = ReleaseOperation(owner, commandId);
        var reference = NativeFacts.Ref();

        // Act
        var noFirst = Record.Exception(() =>
        {
            _ = owner.ReleaseHold(operation, source, "Matter closed", null!, reference);
        });
        var noResult = Record.Exception(() =>
        {
            _ = owner.ReleaseHold(operation, source, "Matter closed", reference, null!);
        });

        // Assert
        Assert.IsType<ArgumentNullException>(noFirst);
        Assert.IsType<ArgumentNullException>(noResult);
        Assert.Equal(0, Pending(owner));
        Assert.Null(owner.Release);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldReturnOriginalResultGivenExactPlacementRetryAndRefuseChangedActor()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = Open(tenantId, holdId, holdId);
        var operation = PlaceOperation(owner);
        Assert.True(Place(owner, operation, source).IsSuccess);
        var changed = operation with { Actor = NativeFacts.Root(owner, holdId, "PlaceArtifactLegalHold", 0).Actor };

        // Act
        var retry = Place(owner, operation, source);
        var changedIntent = Place(owner, changed, source);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.False(changedIntent.IsSuccess);
        Assert.Equal(AcceptedAppendResult.ConflictError, changedIntent.Error);
        Assert.Equal(1, Pending(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreHoldStateGivenValidPlacedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);

        // Act
        var owner = PlacedHold(tenantId, holdId, holdId, source, out var placement);

        // Assert
        Assert.Equal(1, owner.Revision);
        Assert.Equal(source.Source, owner.Source);
        Assert.NotNull(owner.View);
        Assert.Null(owner.View.ReleasedAt);
        Assert.Equal(placement.Actor, owner.View.PlacedBy);
        Assert.Equal(0, Pending(owner));
    }

    [Fact]
    public void ShouldReturnOriginalResultGivenExactPlacementRetryAfterRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = ReleasedHold(tenantId, holdId, Uuid.CreateVersion4(), holdId, source, out var placement, out _);

        // Act
        var retry = Place(owner, placement, source);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(0, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseSecondPlacementGivenHoldAlreadyPlaced()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = PlacedHold(tenantId, holdId, holdId, source, out _);
        var competing = PlaceOperation(owner, 0, PlacedAt.AddDays(1));

        // Act
        var second = Place(owner, competing, source with { Position = 9 });
        var prepared = owner.PreparePlace(source, 0, "Legal matter");

        // Assert
        Assert.False(second.IsSuccess);
        Assert.Equal(AcceptedAppendResult.ConflictError, second.Error);
        Assert.Equal(RequestErrorKind.Conflict, prepared.Error!.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal(source.Source, owner.Source);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldReleaseExactPlacedContentGivenLaterAttributedRelease()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = PlacedHold(tenantId, holdId, commandId, source, out var placement);
        var operation = ReleaseOperation(owner, commandId);

        // Act
        var result = Release(owner, operation, source with { Position = 8 }, "  Matter closed  ");

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var released = ReleasedFact(owner);
        Assert.Equal(source.Source, released.Source);
        Assert.Equal(2, released.Revision);
        Assert.Equal(8UL, released.SourcePosition);
        Assert.Equal(holdId, released.HoldId);
        Assert.Equal("Matter closed", released.Reason);
        Assert.Equal(operation.Actor, released.RecordedBy);
        Assert.Equal(ReleasedAt, released.RecordedAt);
        Assert.Equal(commandId, released.Operation.LogicalId);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(placement.Actor, owner.View!.PlacedBy);
        Assert.Equal(ReleasedAt, owner.View.ReleasedAt);
    }

    [Fact]
    public void ShouldAppendNothingGivenReleaseOfUnplacedHold()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        var owner = Open(tenantId, Uuid.CreateVersion4(), commandId);
        var source = Snapshot(tenantId);

        // Act
        var result = Release(owner, ReleaseOperation(owner, commandId), source);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenReleaseNotMatchingPlacedContentOrRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = PlacedHold(tenantId, holdId, commandId, source, out _);
        var otherContent = source with { Source = source.Source with { ContentSha256 = new string('b', 64) } };
        var otherSource = source with { Source = source.Source with { SourceId = Uuid.CreateVersion4() } };

        // Act
        var differentContent = Release(owner, ReleaseOperation(owner, commandId), otherContent);
        var differentSource = Release(owner, ReleaseOperation(owner, commandId), otherSource);
        var earlierPosition = Release(owner, ReleaseOperation(owner, commandId), source with { Position = 4 });
        var staleRevision = Release(owner, ReleaseOperation(owner, commandId, 0), source);
        var futureRevision = Release(owner, ReleaseOperation(owner, commandId, 2), source);
        var withoutRevision = Release(owner, ReleaseOperation(owner, commandId, null), source);
        var beforePlacement = Release(owner, ReleaseOperation(owner, commandId, 1, PlacedAt.AddDays(-1)), source);
        var noReason = Release(owner, ReleaseOperation(owner, commandId), source, " ");
        var otherLogical = Release(owner, ReleaseOperation(owner, Uuid.CreateVersion4()), source);
        var preparedStale = owner.PrepareRelease(source, 0, "Matter closed", ReleasedAt);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, differentContent.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, differentSource.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, earlierPosition.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, staleRevision.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, futureRevision.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, withoutRevision.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, beforePlacement.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, noReason.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, otherLogical.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, preparedStale.Error!.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Null(owner.Release);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldReturnOriginalResultGivenExactReleaseRetryAndRefuseChangedActor()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = PlacedHold(tenantId, holdId, commandId, source, out _);
        var operation = ReleaseOperation(owner, commandId);
        Assert.True(Release(owner, operation, source).IsSuccess);
        var changed = operation with { Actor = NativeFacts.Root(owner, commandId, "ReleaseArtifactLegalHold", 1).Actor };

        // Act
        var retry = Release(owner, operation, source);
        var changedIntent = Release(owner, changed, source);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.False(changedIntent.IsSuccess);
        Assert.Equal(AcceptedAppendResult.ConflictError, changedIntent.Error);
        Assert.Equal(1, Pending(owner));
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseCompetingReleaseGivenHoldAlreadyReleased()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var firstCommand = Uuid.CreateVersion4();
        var competingCommand = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = ReleasedHold(tenantId, holdId, firstCommand, competingCommand, source, out _, out _);
        var releasedAt = owner.Release!.RecordedAt;

        // Act
        var competing = Release(owner, ReleaseOperation(owner, competingCommand, 1, ReleasedAt.AddDays(1)), source);
        var prepared = owner.PrepareRelease(source, 1, "Matter closed", ReleasedAt.AddDays(1));

        // Assert
        Assert.False(competing.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, competing.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, prepared.Error!.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal(releasedAt, owner.Release.RecordedAt);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldReturnOriginalResultGivenExactReleaseRetryAfterReplayedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var holdId = Uuid.CreateVersion4();
        var commandId = Uuid.CreateVersion4();
        var source = Snapshot(tenantId);
        var owner = ReleasedHold(tenantId, holdId, commandId, commandId, source, out _, out var release);

        // Act
        var retry = Release(owner, release, source);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(2, owner.Revision);
        Assert.NotNull(owner.View!.ReleasedAt);
        Assert.Equal(0, Pending(owner));
    }
}
