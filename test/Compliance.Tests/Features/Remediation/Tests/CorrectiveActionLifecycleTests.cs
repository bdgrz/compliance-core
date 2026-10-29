using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class CorrectiveActionLifecycleTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly Uuid Finding = Uuid.CreateVersion4();
    static readonly Uuid Action = Uuid.CreateVersion4();

    [Fact]
    public void ShouldDeriveStableIdentityFromFindingAndActionGivenSameTuple()
    {
        // Arrange
        var other = Uuid.CreateVersion4();

        // Act
        var same = CorrectiveAction.IdentityOf(Tenant, Program, Finding, Action);
        var otherAction = CorrectiveAction.IdentityOf(Tenant, Program, Finding, other);
        var otherFinding = CorrectiveAction.IdentityOf(Tenant, Program, other, Action);
        var otherTenant = CorrectiveAction.IdentityOf(other, Program, Finding, Action);

        // Assert
        Assert.Equal(same, new CorrectiveAction(Tenant, Program, Finding, Action, Uuid.CreateVersion4()).Id);
        Assert.NotEqual(same, otherAction);
        Assert.NotEqual(same, otherFinding);
        Assert.NotEqual(same, otherTenant);
    }

    [Fact]
    public void ShouldMaterializeAtRevisionOneGivenUnopenedAction()
    {
        // Arrange
        var owner = new CorrectiveAction(Tenant, Program, Finding, Action);
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(MaterializeCorrectiveAction), null);

        // Act
        var result = owner.Materialize(operation, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess);
        var added = Assert.IsType<CorrectiveActionAddedV2>(Assert.Single(new AggregateScenario<CorrectiveAction>(owner).PendingEvents));
        Assert.Equal(1, added.Revision);
        Assert.Equal(Finding, added.FindingId);
        Assert.Equal(owner.Stream, added.Operation.Owner);
        Assert.Equal(1, owner.Revision);
        Assert.True(owner.IsMaterialized);
        Assert.False(owner.IsCompleted);
    }

    [Fact]
    public void ShouldRefuseMaterializationGivenOperationOnAnotherStream()
    {
        // Arrange
        var owner = new CorrectiveAction(Tenant, Program, Finding, Action);
        var foreign = new CorrectiveAction(Tenant, Program, Finding, Uuid.CreateVersion4());
        var operation = NativeFacts.Root(foreign, Uuid.CreateVersion4(), nameof(MaterializeCorrectiveAction), null);

        // Act
        var result = owner.Materialize(operation, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
        Assert.False(owner.IsMaterialized);
    }

    [Fact]
    public void ShouldRefuseSecondMaterializationGivenMaterializedAction()
    {
        // Arrange
        var owner = Materialized();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(MaterializeCorrectiveAction), null);

        // Act
        var result = owner.Materialize(operation, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldCompleteAtRevisionTwoGivenMaterializedActionAndExpectedRevisionOne()
    {
        // Arrange
        var owner = Materialized();
        var member = Uuid.CreateVersion4();

        // Act
        var result = Complete(owner, 1, member);

        // Assert
        Assert.True(result.IsSuccess);
        var completed = Assert.IsType<CorrectiveActionCompletedV2>(Assert.Single(new AggregateScenario<CorrectiveAction>(owner).PendingEvents));
        Assert.Equal(2, completed.Revision);
        Assert.Equal(Action, completed.ActionId);
        Assert.Equal(member, completed.CompletedByMemberId);
        Assert.Equal("Access removed.", completed.ResolutionNotes);
        Assert.Equal(2, owner.Revision);
        Assert.True(owner.IsCompleted);
        Assert.Equal(completed.Metadata.EventId, owner.Latest!.Metadata.EventId);
    }

    [Fact]
    public void ShouldRefuseCompletionGivenActionNotMaterialized()
    {
        // Arrange
        var owner = new CorrectiveAction(Tenant, Program, Finding, Action);

        // Act
        var result = Complete(owner, 0, Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
        Assert.False(owner.IsCompleted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ShouldRefuseCompletionGivenStaleExpectedRevision(long expected)
    {
        // Arrange
        var owner = Materialized();

        // Act
        var result = Complete(owner, expected, Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
        Assert.False(owner.IsCompleted);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseCompletionGivenOperationWithoutExpectedRevision()
    {
        // Arrange
        var owner = Materialized();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(CompleteCorrectiveAction), null);

        // Act
        var result = owner.Complete(operation, "Access removed.", NativeFacts.Ref(), Uuid.CreateVersion4(),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"), NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseSecondCompletionGivenCompletedActionReplayed()
    {
        // Arrange
        var history = CompletedHistory(Uuid.CreateVersion4());
        var replayed = NativeFacts.Replay(new CorrectiveAction(Tenant, Program, Finding, Action), history);
        var revision = replayed.Revision;
        var latest = replayed.Latest!.Metadata.EventId;

        // Act
        var result = Complete(replayed, revision, Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Contains("already completed", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(replayed).PendingEvents);
        Assert.Equal(2, replayed.Revision);
        Assert.Equal(latest, replayed.Latest!.Metadata.EventId);
    }

    [Fact]
    public void ShouldRefuseCompletionGivenActiveFindingClosureUse()
    {
        // Arrange
        var source = Materialized();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Closer");
        var parent = new FindingAggregate(Tenant, Program, Finding).Stream;
        var operation = new OperationProvenance(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, "Finding closure use",
            actor, source.Stream, "initial", 1, new string('a', 64), NativeFacts.Now, source.Stream, source.Stream, parent);
        var held = new CorrectiveActionFindingClosureUseAuthorized(source.Stream, 1, 1, NativeFacts.Ref(), null,
            operation, null, null);
        var history = new DomainEvent[] { source.Added!, DomainEventSeed.Attach(held, source.Id, 2) };
        var owner = NativeFacts.Replay(new CorrectiveAction(Tenant, Program, Finding, Action), history);

        // Act
        var result = Complete(owner, 1, Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(owner.Uses.HasActiveUse);
        Assert.False(owner.IsCompleted);
        Assert.Equal(1, owner.Revision);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRestoreLifecycleGivenValidMaterializedAndCompletedHistory()
    {
        // Arrange
        var member = Uuid.CreateVersion4();
        var history = CompletedHistory(member);

        // Act
        var replayed = NativeFacts.Replay(new CorrectiveAction(Tenant, Program, Finding, Action), history);

        // Assert
        Assert.Equal(2, replayed.Revision);
        Assert.True(replayed.IsMaterialized);
        Assert.True(replayed.IsCompleted);
        Assert.Equal(member, replayed.Completed!.CompletedByMemberId);
        Assert.Equal(history[0].Metadata.EventId, replayed.Added!.Metadata.EventId);
        Assert.Equal(history[1].Metadata.EventId, replayed.Latest!.Metadata.EventId);
        Assert.Null(replayed.ClosedByGeneration);
        Assert.False(replayed.Uses.HasActiveUse);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(replayed).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseMaterializationGivenReplayedMaterializedAction()
    {
        // Arrange
        var source = Materialized();
        var replayed = NativeFacts.Replay(new CorrectiveAction(Tenant, Program, Finding, Action),
            new DomainEvent[] { source.Added! });

        // Act
        var result = replayed.PrepareMaterialize();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(replayed).PendingEvents);
    }

    static CorrectiveAction Materialized()
    {
        var seed = new CorrectiveAction(Tenant, Program, Finding, Action);
        var operation = NativeFacts.Root(seed, Uuid.CreateVersion4(), nameof(MaterializeCorrectiveAction), null);
        Assert.True(seed.Materialize(operation, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        return NativeFacts.Replay(new CorrectiveAction(Tenant, Program, Finding, Action), NativeFacts.Raised(seed));
    }

    static DomainEvent[] CompletedHistory(Uuid member)
    {
        var seed = new CorrectiveAction(Tenant, Program, Finding, Action);
        var operation = NativeFacts.Root(seed, Uuid.CreateVersion4(), nameof(MaterializeCorrectiveAction), null);
        Assert.True(seed.Materialize(operation, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        Assert.True(Complete(seed, 1, member).IsSuccess);
        return NativeFacts.Raised(seed);
    }

    static Result Complete(CorrectiveAction owner, long expected, Uuid member)
    {
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(CompleteCorrectiveAction), expected,
            member);
        return owner.Complete(operation, "Access removed.", NativeFacts.Ref(), member,
            ActorReference.ForMember(member, "Owner"), NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());
    }
}
