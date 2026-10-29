using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

/// <summary>Owner-level decisions of one control occurrence, without handlers or content stores.</summary>
public sealed class ControlOccurrenceAggregateTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly Uuid Control = Uuid.CreateVersion4();
    static readonly ControlCalendarWindow Window = new(new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 1));

    [Fact]
    public void ShouldAddressStreamByTenantProgramControlAndWindowGivenAnyOpener()
    {
        // Arrange
        var first = Fresh();
        var again = Fresh();

        // Act
        var other = new ControlOccurrence(Tenant, Program, Control, new ControlCalendarWindow(Window.From,
            Window.UntilExclusive.AddDays(1)));

        // Assert
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Stream, again.Stream);
        Assert.NotEqual(first.Id, other.Id);
        Assert.Equal(ControlOccurrence.Area, first.Stream.Area);
    }

    [Fact]
    public void ShouldMaterializeOnceGivenOpenAndRefuseSecondOpening()
    {
        // Arrange
        var owner = Fresh();
        Assert.True(owner.Open(Root(owner, "OpenControlOccurrence", 0), Terms(), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref()).IsSuccess);
        var events = Pending(owner).Length;

        // Act
        var again = owner.Open(Root(owner, "OpenControlOccurrence", 1), Terms(), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref());
        var receiver = owner.Materialize(Root(owner, "MaterializeControlOccurrence", null), Terms(),
            NativeFacts.Ref());

        // Assert
        Assert.Equal(1, events);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, receiver.Error?.Kind);
        Assert.Single(Pending(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenAttestationOfUnmaterializedOccurrenceWithoutMaterialization()
    {
        // Arrange
        var owner = Fresh();

        // Act
        var result = owner.Attest(Root(owner, "AttestControlOccurrence", 0), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref(), null);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(Pending(owner));
    }

    [Fact]
    public void ShouldMaterializeAndAttestInOneBatchGivenExpectedOccurrenceNotYetOpened()
    {
        // Arrange
        var owner = Fresh();
        var materialization = new ControlOccurrenceMaterialization(Root(owner, "MaterializeControlOccurrence", null),
            Terms(), NativeFacts.Ref());

        // Act
        var result = owner.Attest(Root(owner, "AttestControlOccurrence", 0), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref(), materialization);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<ControlOccurrenceOpened>(Pending(owner)[0]);
        Assert.IsType<ControlOccurrenceAttested>(Pending(owner)[1]);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(1, owner.AttestationCount);
    }

    [Fact]
    public void ShouldAppendNothingGivenReviewBeforeAnyAttestation()
    {
        // Arrange
        var owner = Opened();
        var before = Pending(owner).Length;

        // Act
        var result = owner.Review(Root(owner, "ReviewControlOccurrence", 1), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before, Pending(owner).Length);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenStaleExpectedRevisionOnAttestationAndReview()
    {
        // Arrange
        var owner = Opened();
        Assert.True(owner.Attest(Root(owner, "AttestControlOccurrence", 1), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref(), null).IsSuccess);
        var before = Pending(owner).Length;

        // Act
        var staleAttest = owner.Attest(Root(owner, "AttestControlOccurrence", 1), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(), null);
        var staleReview = owner.Review(Root(owner, "ReviewControlOccurrence", 1), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(staleAttest.IsSuccess);
        Assert.False(staleReview.IsSuccess);
        Assert.Equal(before, Pending(owner).Length);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseGivenOperationNotRootedOnOwnStreamOrWithoutExpectedRevision()
    {
        // Arrange
        var owner = Opened();
        var other = Fresh(new ControlCalendarWindow(Window.From.AddYears(1), Window.UntilExclusive.AddYears(1)));
        var before = Pending(owner).Length;

        // Act
        var foreign = owner.Attest(Root(other, "AttestControlOccurrence", 1), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref(), null);
        var unversioned = owner.Review(Root(owner, "ReviewControlOccurrence", null), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, foreign.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, unversioned.Error?.Kind);
        Assert.Equal(before, Pending(owner).Length);
    }

    [Fact]
    public void ShouldKeepEveryAttestationVersionAndReviewGivenCorrectionHistory()
    {
        // Arrange
        var owner = Opened();
        var first = owner.NextAttestationId;

        // Act
        Assert.True(owner.Attest(Root(owner, "AttestControlOccurrence", 1), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref(), null).IsSuccess);
        var second = owner.NextAttestationId;
        Assert.True(owner.Attest(Root(owner, "AttestControlOccurrence", 2), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref(), null).IsSuccess);
        var reviewed = owner.Review(Root(owner, "ReviewControlOccurrence", 3), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref());

        // Assert
        Assert.True(reviewed.IsSuccess);
        Assert.NotEqual(first, second);
        Assert.Equal(2, owner.AttestationCount);
        Assert.Equal(4, owner.Revision);
        long[] expectedRevisions = [1L, 2L, 3L, 4L];
        Assert.Equal(expectedRevisions, Pending(owner).Select(RevisionOf).ToArray());
    }

    [Fact]
    public void ShouldRestoreStateGivenValidHistoryAndRefuseStaleCommandAfterReplay()
    {
        // Arrange
        var owner = Opened();
        Assert.True(owner.Attest(Root(owner, "AttestControlOccurrence", 1), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref(), null).IsSuccess);
        var history = NativeFacts.Raised(owner);

        // Act
        var replayed = NativeFacts.Replay(Fresh(), history);
        var revision = replayed.Revision;
        var attestations = replayed.AttestationCount;
        var stale = replayed.Review(Root(replayed, "ReviewControlOccurrence", 1), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref());
        var current = replayed.Review(Root(replayed, "ReviewControlOccurrence", 2), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(2, revision);
        Assert.Equal(1, attestations);
        Assert.NotNull(replayed.Opened);
        Assert.False(stale.IsSuccess);
        Assert.True(current.IsSuccess);
    }

    static ControlOccurrence Fresh(ControlBusinessWindow? window = null) =>
        new(Tenant, Program, Control, window ?? Window);

    static ControlOccurrence Opened()
    {
        var owner = Fresh();
        Assert.True(owner.Open(Root(owner, "OpenControlOccurrence", 0), Terms(), NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref()).IsSuccess);
        return owner;
    }

    static DomainEvent[] Pending(ControlOccurrence owner) =>
        new AggregateScenario<ControlOccurrence>(owner).PendingEvents.ToArray();

    static long RevisionOf(DomainEvent fact) => fact switch
    {
        ControlOccurrenceOpened opened => opened.Revision,
        ControlOccurrenceAttested attested => attested.Revision,
        ControlOccurrenceReviewed reviewed => reviewed.Revision,
        _ => throw new InvalidOperationException("Not an occurrence fact."),
    };

    static OperationProvenance Root(Aggregate owner, string commandKind, long? expectedRevision) =>
        NativeFacts.Root(owner, Uuid.CreateVersion4(), commandKind, expectedRevision);

    static ControlOccurrenceTerms Terms() => new("recurring", Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 15), null,
        new OperatingHolder("member", Uuid.CreateVersion4()), ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"),
        NativeFacts.Now, Window);
}
