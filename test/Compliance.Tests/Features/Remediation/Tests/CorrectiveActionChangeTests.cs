using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class CorrectiveActionChangeTests
{
    static readonly Uuid Member = Uuid.CreateVersion4();
    static readonly ActorReference Actor = ActorReference.ForMember(Member, "Lead");
    static readonly DateOnly Due = new(2026, 12, 1);

    [Fact]
    public void ShouldChangeEditableTermsAtNextRevisionGivenOpenAction()
    {
        // Arrange
        var owner = Open();
        var member = Uuid.CreateVersion4();

        // Act
        var result = Change(owner, "  Remove SSO and VPN access.  ", member, Due);

        // Assert
        Assert.True(result.IsSuccess);
        var fact = Assert.IsType<CorrectiveActionChanged>(Assert.Single(new AggregateScenario<CorrectiveAction>(owner).PendingEvents));
        Assert.Equal(2, owner.Revision);
        Assert.Equal("Remove SSO and VPN access.", fact.Description);
        Assert.Equal(member, fact.OwnerMemberId);
        Assert.Equal(Due, fact.DueOn);
        Assert.Equal(Actor, fact.ChangedBy);
        Assert.Equal(fact, owner.Changed);
        Assert.False(owner.IsCompleted);
        Assert.False(owner.IsCancelled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldRefuseWithoutFactGivenChangeHasNoDescription(string description)
    {
        // Arrange
        var owner = Open();

        // Act
        var result = Change(owner, description, Member, Due);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldRefuseWithoutFactGivenChangeHasMissingOwnerOrDueDate(bool missingOwner)
    {
        // Arrange
        var owner = Open();

        // Act
        var result = Change(owner, "Remove access.", missingOwner ? Uuid.Empty : Member, missingOwner ? Due : default);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ShouldRefuseWithoutFactGivenChangeHasStaleRevision(long revision)
    {
        // Arrange
        var owner = Open();

        // Act
        var result = Change(owner, "Remove access.", Member, Due, revision);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, owner.Revision);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseChangeGivenActionNotMaterialized()
    {
        // Arrange
        var owner = NewAction();

        // Act
        var result = Change(owner, "Remove access.", Member, Due, 0);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldRefuseChangeGivenActionAlreadyResolved(bool completed)
    {
        // Arrange
        var owner = Open();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), completed ? nameof(CompleteCorrectiveAction) : nameof(CancelCorrectiveAction), 1, Member);
        var terminal = completed
            ? owner.Complete(operation, "Removed.", NativeFacts.Ref(), Member, Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref())
            : owner.Cancel(operation, "Duplicate work.", Member, Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());
        Assert.True(terminal.IsSuccess);
        new AggregateScenario<CorrectiveAction>(owner).Given();

        // Act
        var result = Change(owner, "Changed after resolution.", Member, Due, owner.Revision);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRestoreLatestTermsGivenChangedActionHistoryReplayed()
    {
        // Arrange
        var owner = NewAction();
        Assert.True(owner.Materialize(NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(MaterializeCorrectiveAction), null),
            NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        Assert.True(Change(owner, "First change.", Member, Due).IsSuccess);
        var nextOwner = Uuid.CreateVersion4();
        Assert.True(Change(owner, "Second change.", nextOwner, Due.AddDays(2), 2).IsSuccess);
        var history = NativeFacts.Raised(owner);

        // Act
        var replayed = NativeFacts.Replay(new CorrectiveAction(owner.Added!.TenantId, owner.Added.ProgramId,
            owner.FindingId, owner.ActionId), history);

        // Assert
        Assert.Equal(3, replayed.Revision);
        Assert.Equal("Second change.", replayed.Changed!.Description);
        Assert.Equal(nextOwner, replayed.Changed.OwnerMemberId);
        Assert.Equal(Due.AddDays(2), replayed.Changed.DueOn);
    }

    [Fact]
    public void ShouldRefuseChangeGivenActiveFindingClosureUse()
    {
        // Arrange
        var seed = Open();
        var parent = new FindingAggregate(seed.Added!.TenantId, seed.Added.ProgramId, seed.FindingId).Stream;
        var operation = new OperationProvenance(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, "Finding closure use",
            Actor, seed.Stream, "initial", 1, new string('a', 64), NativeFacts.Now, seed.Stream, seed.Stream, parent);
        var held = DomainEventSeed.Attach(new CorrectiveActionFindingClosureUseAuthorized(seed.Stream, 1, 1,
            NativeFacts.Ref(), null, operation, null, null), seed.Id, 2);
        var owner = NativeFacts.Replay(new CorrectiveAction(seed.Added.TenantId, seed.Added.ProgramId,
            seed.FindingId, seed.ActionId), new DomainEvent[] { seed.Added, held });

        // Act
        var result = Change(owner, "Changed while held.", Member, Due);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.True(owner.Uses.HasActiveUse);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    static CorrectiveAction NewAction() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

    static CorrectiveAction Open()
    {
        var owner = NewAction();
        Assert.True(owner.Materialize(NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(MaterializeCorrectiveAction), null),
            NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        new AggregateScenario<CorrectiveAction>(owner).Given();
        return owner;
    }

    static Result Change(CorrectiveAction owner, string description, Uuid member, DateOnly due, long revision = 1) =>
        owner.Change(NativeFacts.Root(owner, Uuid.CreateVersion4(), nameof(ChangeCorrectiveAction), revision, Member) with { Actor = Actor },
            description, member, due, Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());
}
