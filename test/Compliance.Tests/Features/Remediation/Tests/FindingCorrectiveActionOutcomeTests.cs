using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingCorrectiveActionOutcomeTests
{
    static readonly ActorReference Lead = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
    static readonly ActorReference Reporter = ActorReference.ForSystemProcess("report_corrective_action_outcome", "Report action outcome");

    [Fact]
    public void ShouldRefuseClosureWithoutFactGivenAdmittedActionHasNoReportedOutcome()
    {
        // Arrange
        var owner = Planned(out _);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.AcceptClosure(Root(owner, Uuid.CreateVersion4(), nameof(CloseFinding), owner.Revision),
            NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(owner.PendingClosure);
        Assert.Equal(1, owner.UnresolvedActionCount);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("cancelled")]
    public void ShouldPermitClosureGivenEveryAdmittedActionHasReportedTerminalOutcome(string outcome)
    {
        // Arrange
        var owner = Planned(out var action);
        var source = Source(owner, action);

        // Act
        var report = owner.RecordActionOutcome(Report(owner, source), action, outcome, source, Proof());
        var closure = owner.AcceptClosure(Root(owner, Uuid.CreateVersion4(), nameof(CloseFinding), owner.Revision),
            NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.True(report.IsSuccess);
        Assert.True(closure.IsSuccess);
        Assert.Equal(0, owner.UnresolvedActionCount);
        var resolved = Assert.Single(new AggregateScenario<FindingAggregate>(owner).PendingEvents.OfType<FindingCorrectiveActionResolved>());
        Assert.Equal(action, resolved.ActionId);
        Assert.Equal(outcome, resolved.Outcome);
        Assert.Equal(source, resolved.Source);
    }

    [Fact]
    public void ShouldKeepClosureRefusedGivenOnlyOneOfTwoActionsResolved()
    {
        // Arrange
        var owner = Planned(out var first);
        var second = Uuid.CreateVersion4();
        Assert.True(owner.AdmitAction(Root(owner, second, nameof(AddCorrectiveAction), owner.Revision),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref()).IsSuccess);
        var source = Source(owner, first);
        Assert.True(owner.RecordActionOutcome(Report(owner, source), first, "completed", source, Proof()).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.PrepareClosure(owner.Revision);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, owner.UnresolvedActionCount);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldKeepExactRepeatedOutcomeAsNoOpGivenFindingAlreadyClosingOrClosed(bool closed)
    {
        // Arrange
        var owner = Planned(out var action);
        var source = Source(owner, action);
        var operation = Report(owner, source);
        var proof = Proof();
        Assert.True(owner.RecordActionOutcome(operation, action, "completed", source, proof).IsSuccess);
        Assert.True(owner.AcceptClosure(Root(owner, Uuid.CreateVersion4(), nameof(CloseFinding), owner.Revision),
            NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref()).IsSuccess);
        if (closed)
            Assert.True(owner.CompleteClosure(owner.PendingClosure!.Operation with
            {
                LogicalId = Uuid.CreateVersion4(),
                CommandKind = nameof(CompleteFindingClosure),
            }, NativeFacts.Ref(), NativeFacts.Ref(), owner.PendingClosure!.Intent, NativeFacts.Ref()).IsSuccess);
        var revision = owner.Revision;
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.RecordActionOutcome(operation, action, "completed", source, proof);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(revision, owner.Revision);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseChangedOutcomeWithoutFactGivenActionAlreadyResolved()
    {
        // Arrange
        var owner = Planned(out var action);
        var source = Source(owner, action);
        Assert.True(owner.RecordActionOutcome(Report(owner, source), action, "completed", source, Proof()).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.RecordActionOutcome(Report(owner, source), action, "cancelled", source, Proof());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, owner.UnresolvedActionCount);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenOutcomeForUnplannedAction()
    {
        // Arrange
        var owner = Planned(out _);
        var action = Uuid.CreateVersion4();
        var source = Source(owner, action);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.RecordActionOutcome(Report(owner, source), action, "completed", source, Proof());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, owner.UnresolvedActionCount);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRestoreResolvedActionGuardGivenHistoryReplayed()
    {
        // Arrange
        var owner = Planned(out var action);
        var source = Source(owner, action);
        Assert.True(owner.RecordActionOutcome(Report(owner, source), action, "cancelled", source, Proof()).IsSuccess);
        var history = NativeFacts.Raised(owner);

        // Act
        var replayed = NativeFacts.Replay(new FindingAggregate(owner.TenantId, owner.ProgramId, owner.FindingId), history);
        var closure = replayed.PrepareClosure(replayed.Revision);

        // Assert
        Assert.Equal(1, replayed.AdmittedCount);
        Assert.Equal(0, replayed.UnresolvedActionCount);
        Assert.True(closure.IsSuccess);
        Assert.Empty(new AggregateScenario<FindingAggregate>(replayed).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenOutcomeReferencesAnotherActionOwner()
    {
        // Arrange
        var owner = Planned(out var action);
        var source = Source(owner, Uuid.CreateVersion4());
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.RecordActionOutcome(Report(owner, source), action, "completed", source, Proof());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1, owner.UnresolvedActionCount);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldPreserveClosureAdmissionRuleGivenPlanningAndClosureAreOrdered(bool closureFirst)
    {
        // Arrange
        var owner = Planned(out var action);
        var source = Source(owner, action);
        Assert.True(owner.RecordActionOutcome(Report(owner, source), action, "completed", source, Proof()).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();
        var next = Uuid.CreateVersion4();

        // Act
        var first = closureFirst
            ? owner.AcceptClosure(Root(owner, Uuid.CreateVersion4(), nameof(CloseFinding), owner.Revision),
                NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref())
            : owner.AdmitAction(Root(owner, next, nameof(AddCorrectiveAction), owner.Revision),
                NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref());
        var second = closureFirst
            ? owner.AdmitAction(Root(owner, next, nameof(AddCorrectiveAction), owner.Revision),
                NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref())
            : owner.AcceptClosure(Root(owner, Uuid.CreateVersion4(), nameof(CloseFinding), owner.Revision),
                NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.True(first.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Single(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
        Assert.Equal(closureFirst ? 0 : 1, owner.UnresolvedActionCount);
    }

    static FindingAggregate Planned(out Uuid action)
    {
        action = Uuid.CreateVersion4();
        var owner = new FindingAggregate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), action);
        var terms = new FindingRaiseTerms(new FindingSource("manual", null, null, "Original source wording."),
            "Retained access", "Leaver access survived.", "high", "VPN", Uuid.CreateVersion4(),
            new DateOnly(2026, 12, 1), Lead, NativeFacts.Now, "manual", null);
        Assert.True(owner.Raise(Root(owner, owner.FindingId, nameof(RaiseFinding), null), terms,
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), null).IsSuccess);
        Assert.True(owner.AdmitAction(Root(owner, action, nameof(AddCorrectiveAction), owner.Revision),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref()).IsSuccess);
        return owner;
    }

    static OperationProvenance Root(FindingAggregate owner, Uuid id, string kind, long? revision) =>
        FindingContent.Provenance(id, kind, owner.Stream, Lead, revision is { } number ? (ulong)number : null,
            new string('a', 64), NativeFacts.Now) with { FirstOwner = owner.Stream };

    static NativeFactReference Source(FindingAggregate owner, Uuid action) => new(
        new CorrectiveAction(owner.TenantId, owner.ProgramId, owner.FindingId, action).Stream, Uuid.CreateVersion4(), 2);

    static ImmutableContentRef Proof() => NativeFacts.Ref(SourceFactProofs.RequiredFactsSchema);

    static OperationProvenance Report(FindingAggregate owner, NativeFactReference source) =>
        FindingContent.Provenance(Uuid.CreateVersion5(source.EventId, nameof(RecordCorrectiveActionOutcome)),
            nameof(RecordCorrectiveActionOutcome), owner.Stream, Reporter, null, new string('b', 64), NativeFacts.Now)
        with { FirstOwner = owner.Stream, CausationEventId = source.EventId };
}
