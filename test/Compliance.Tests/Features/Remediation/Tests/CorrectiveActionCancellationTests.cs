using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class CorrectiveActionCancellationTests
{
    static readonly ActorReference Lead = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");

    [Fact]
    public void ShouldCancelAtNextRevisionGivenOpenActionAndReason()
    {
        // Arrange
        var owner = Open();

        // Act
        var result = Cancel(owner, "  Duplicate action; retained action fixes the issue.  ");

        // Assert
        Assert.True(result.IsSuccess);
        var fact = Assert.IsType<CorrectiveActionCancelled>(Assert.Single(new AggregateScenario<CorrectiveAction>(owner).PendingEvents));
        Assert.Equal(2, owner.Revision);
        Assert.True(owner.IsCancelled);
        Assert.False(owner.IsCompleted);
        Assert.Equal("Duplicate action; retained action fixes the issue.", fact.Reason);
        Assert.Equal(Lead, fact.CancelledBy);
        Assert.Equal(owner.ActionId, fact.ActionId);
        Assert.Equal(fact, owner.Latest);
        Assert.Equal(fact, owner.ContentFactById(fact.Metadata.EventId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldRefuseWithoutFactGivenMissingCancellationReason(string reason)
    {
        // Arrange
        var owner = Open();

        // Act
        var result = Cancel(owner, reason);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.False(owner.IsCancelled);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenCancellationReasonExceedsLimit()
    {
        // Arrange
        var owner = Open();

        // Act
        var result = Cancel(owner, new string('x', 4001));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ShouldRefuseWithoutFactGivenStaleCancellationRevision(long expectedRevision)
    {
        // Arrange
        var owner = Open();

        // Act
        var result = Cancel(owner, "Duplicate action.", expectedRevision);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseCancellationGivenActionNotMaterialized()
    {
        // Arrange
        var owner = NewAction();

        // Act
        var result = Cancel(owner, "Duplicate action.", 0);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseCancellationGivenCompletedAction()
    {
        // Arrange
        var owner = Open();
        Assert.True(owner.Complete(NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(CompleteCorrectiveAction), 1),
            "Access removed.", NativeFacts.Ref(), MemberId(), Lead, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        new AggregateScenario<CorrectiveAction>(owner).Given();

        // Act
        var result = Cancel(owner, "Duplicate action.", 2);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(owner.IsCompleted);
        Assert.False(owner.IsCancelled);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRestoreTerminalCancellationGivenHistoryReplayed()
    {
        // Arrange
        var seed = Open();
        Assert.True(Cancel(seed, "Duplicate action.").IsSuccess);
        var facts = new DomainEvent[] { seed.Added!, seed.Cancelled! };

        // Act
        var owner = NativeFacts.Replay(new CorrectiveAction(Uuid.Parse(seed.Stream.Realm, CultureInfo.InvariantCulture),
            seed.Added!.ProgramId, seed.FindingId, seed.ActionId), facts);
        var second = Cancel(owner, "New reason.", 2);
        var complete = owner.PrepareComplete(2);

        // Assert
        Assert.True(owner.IsCancelled);
        Assert.False(owner.IsCompleted);
        Assert.Equal(2, owner.Revision);
        Assert.Equal("Duplicate action.", owner.Cancelled!.Reason);
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, complete.Error?.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseCancellationGivenActiveFindingClosureUse()
    {
        // Arrange
        var seed = Open();
        var parent = new FindingAggregate(Uuid.Parse(seed.Stream.Realm, CultureInfo.InvariantCulture), seed.Added!.ProgramId, seed.FindingId).Stream;
        var operation = new OperationProvenance(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, "Finding closure use",
            Lead, seed.Stream, "initial", 1, new string('a', 64), NativeFacts.Now, seed.Stream, seed.Stream, parent);
        var held = DomainEventSeed.Attach(new CorrectiveActionFindingClosureUseAuthorized(seed.Stream, 1, 1,
            NativeFacts.Ref(), null, operation, null, null), seed.Id, 2);
        var owner = NativeFacts.Replay(new CorrectiveAction(Uuid.Parse(seed.Stream.Realm, CultureInfo.InvariantCulture), seed.Added.ProgramId,
            seed.FindingId, seed.ActionId), new DomainEvent[] { seed.Added, held });

        // Act
        var result = Cancel(owner, "Duplicate action.");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.True(owner.Uses.HasActiveUse);
        Assert.False(owner.IsCancelled);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenCancellationOperationBelongsToAnotherAction()
    {
        // Arrange
        var owner = Open();
        var foreign = Open();
        var operation = NativeFacts.Root(foreign, Uuid.CreateVersion4(), nameof(CancelCorrectiveAction), 1);

        // Act
        var result = owner.Cancel(operation, "Duplicate action.", MemberId(), Lead, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenCancellationHasNoExpectedRevision()
    {
        // Arrange
        var owner = Open();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(CancelCorrectiveAction), null);

        // Act
        var result = owner.Cancel(operation, "Duplicate action.", MemberId(), Lead, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldRejectCancellationFactGivenWrongIdentityOrMissingReason(bool wrongIdentity)
    {
        // Arrange
        var source = Open();
        Assert.True(Cancel(source, "Duplicate action.").IsSuccess);
        var cancelled = source.Cancelled! with
        {
            TenantId = wrongIdentity ? Uuid.CreateVersion4() : source.Cancelled!.TenantId,
            Reason = wrongIdentity ? source.Cancelled!.Reason : " ",
        };
        var owner = new AggregateScenario<CorrectiveAction>(new CorrectiveAction(source.Added!.TenantId,
            source.Added.ProgramId, source.FindingId, source.ActionId));

        // Act
        var failure = Record.Exception(() => owner.Given(source.Added, cancelled));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    static CorrectiveAction NewAction() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), Uuid.CreateVersion4());

    static CorrectiveAction Open()
    {
        var owner = NewAction();
        Assert.True(owner.Materialize(NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(MaterializeCorrectiveAction), null),
            NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        new AggregateScenario<CorrectiveAction>(owner).Given();
        return owner;
    }

    static Uuid MemberId() => Uuid.Parse(Lead.Id, CultureInfo.InvariantCulture);

    static Result Cancel(CorrectiveAction owner, string reason, long revision = 1) => owner.Cancel(
        NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(CancelCorrectiveAction), revision, MemberId()),
        reason, MemberId(), Lead, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());
}
