using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaLabelReleaseRulesTests
{
    static readonly ActorAttribution Actor = new("operator", "issuer");

    [Fact]
    public void ShouldAuthorizeDelayedReservationCleanupGivenTheExactDraftCancellation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var request = Fact(new CriteriaEditionDraftRequested(edition, operation, operation, 1,
            "tsc", "edition_1", Actor, null, null, null));
        var reservation = Fact(new EditionLabelReserved("edition_1", edition, operation, 3,
            request.Metadata.EventId, Actor));
        var cancelled = Fact(new CriteriaEditionDraftRequestCancelled(edition, Uuid.CreateVersion4(), 1,
            operation, Actor, null!), version: 2);

        // Act
        var authorization = CriteriaLabelReleaseRules.Authorize(reservation, request, cancelled);

        // Assert
        Assert.Equal("cancelled", authorization!.Value.Reason);
        Assert.Equal(Uuid.Empty, authorization.Value.ReplacementConfirmationEventId);
    }

    [Fact]
    public void ShouldRejectCleanupGivenACancellationForAnotherOperation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var request = Fact(new CriteriaEditionDraftRequested(edition, operation, operation, 1,
            "tsc", "edition_1", Actor, null, null, null));
        var reservation = Fact(new EditionLabelReserved("edition_1", edition, operation, 1,
            request.Metadata.EventId, Actor));
        var cancelled = Fact(new CriteriaEditionDraftRequestCancelled(edition, Uuid.CreateVersion4(), 1,
            Uuid.CreateVersion4(), Actor, null!), version: 2);

        // Act
        var authorization = CriteriaLabelReleaseRules.Authorize(reservation, request, cancelled);

        // Assert
        Assert.Null(authorization);
    }

    [Fact]
    public void ShouldAuthorizeTargetFenceWithoutObservingAReservationGivenExactCancellation()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var request = Fact(new CriteriaEditionDraftRequested(edition, operation, operation, 1,
            "tsc", "edition_1", Actor, null, null, null));
        var cancelled = Fact(new CriteriaEditionDraftRequestCancelled(edition, Uuid.CreateVersion4(), 1,
            operation, Actor, null!), version: 2);

        // Act
        var reason = CriteriaLabelReleaseRules.AuthorizeFence(edition, operation, "edition_1", request, cancelled);
        var wrongLabel = CriteriaLabelReleaseRules.AuthorizeFence(edition, operation, "edition_2", request, cancelled);
        var wrongOperation = CriteriaLabelReleaseRules.AuthorizeFence(edition, Uuid.CreateVersion4(), "edition_1",
            request, cancelled);

        // Assert
        Assert.Equal("cancelled", reason);
        Assert.Null(wrongLabel);
        Assert.Null(wrongOperation);
    }

    [Fact]
    public void ShouldRejectTargetFenceGivenATerminalFactOlderThanTheRequest()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var request = Fact(new CriteriaEditionDraftRequested(edition, operation, operation, 1,
            "tsc", "edition_1", Actor, null, null, null), version: 2);
        var cancelled = Fact(new CriteriaEditionDraftRequestCancelled(edition, Uuid.CreateVersion4(), 1,
            operation, Actor, null!));

        // Act
        var reason = CriteriaLabelReleaseRules.AuthorizeFence(edition, operation, "edition_1", request, cancelled);

        // Assert
        Assert.Null(reason);
    }

    [Fact]
    public void ShouldAuthorizeOldClaimReleaseOnlyGivenTheExactConfirmedRename()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var oldOperation = Uuid.CreateVersion4();
        var newOperation = Uuid.CreateVersion4();
        var original = Fact(new CriteriaEditionDraftRequested(edition, oldOperation, oldOperation, 1,
            "tsc", "edition_1", Actor, null, null, null));
        var reservation = Fact(new EditionLabelReserved("edition_1", edition, oldOperation, 4,
            original.Metadata.EventId, Actor));
        var renameIntent = Fact(new CriteriaEditionRenameRequested(edition, newOperation, 1,
            "edition_1", 4, "edition_2", Actor, null!), version: 2);
        var replacementReservation = Fact(new EditionLabelReserved("edition_2", edition, newOperation, 2,
            renameIntent.Metadata.EventId, Actor));
        var rename = Fact(new CriteriaEditionDraftRenamed(edition, newOperation, "edition_1", 4,
            "edition_2", 2, replacementReservation.Metadata.EventId), version: 3);
        var confirmation = Fact(new EditionLabelConfirmed("edition_2", edition, newOperation, 2,
            rename.Metadata.EventId), version: 2);
        var completed = Fact(new CriteriaEditionRenameCompleted(edition, newOperation, "edition_2", 2,
            confirmation.Metadata.EventId), version: 4);

        // Act
        var authorized = CriteriaLabelReleaseRules.Authorize(reservation, original, completed, rename, confirmation,
            replacementReservation, renameIntent);
        var noConfirmation = CriteriaLabelReleaseRules.Authorize(reservation, original, completed, rename, null);
        var differentGeneration = CriteriaLabelReleaseRules.Authorize(reservation, original,
            completed with { LabelGeneration = 3 }, rename, confirmation, replacementReservation, renameIntent);
        var noReservation = CriteriaLabelReleaseRules.Authorize(reservation, original, completed, rename, confirmation,
            null, renameIntent);

        // Assert
        Assert.Equal("renamed", authorized!.Value.Reason);
        Assert.Equal(confirmation.Metadata.EventId, authorized.Value.ReplacementConfirmationEventId);
        Assert.Null(noConfirmation);
        Assert.Null(differentGeneration);
        Assert.Null(noReservation);
    }

    [Fact]
    public void ShouldRejectAbandonmentGivenAnEarlierClaimGeneration()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var request = Fact(new CriteriaEditionDraftRequested(edition, operation, operation, 1,
            "tsc", "edition_1", Actor, null, null, null));
        var reservation = Fact(new EditionLabelReserved("edition_1", edition, operation, 2,
            request.Metadata.EventId, Actor));
        var abandoned = Fact(new CriteriaEditionDraftAbandoned(edition, operation, Uuid.CreateVersion4(), 1,
            "edition_1", 1, Actor, null!), version: 2);

        // Act
        var authorization = CriteriaLabelReleaseRules.Authorize(reservation, request, abandoned);

        // Assert
        Assert.Null(authorization);
    }

    static T Fact<T>(T fact, ulong version = 1) where T : DomainEvent
    {
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(), version, DateTimeOffset.UtcNow));
        return fact;
    }
}
