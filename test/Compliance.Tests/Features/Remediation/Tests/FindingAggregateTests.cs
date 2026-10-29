using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingAggregateTests
{
    static readonly ActorReference Lead = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");

    [Fact]
    public void ShouldRaiseAtRevisionOneGivenOwnFindingOperation()
    {
        // Arrange
        var owner = Open(out var id);

        // Act
        var result = owner.Raise(Op(owner, id, nameof(RaiseFinding), null), Terms(), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(), null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(1, owner.Generation);
        Assert.Equal(1, Pending(owner));
        Assert.Equal(FindingAggregate.StreamId(owner.ProgramId, id), owner.Id);
    }

    [Fact]
    public void ShouldAdmitInitialActionAtomicallyGivenRoutedRaise()
    {
        // Arrange
        var owner = Open(out var id);
        var initial = new InitialActionAdmission(Op(owner, Uuid.CreateVersion4(), nameof(AddCorrectiveAction), null),
            NativeFacts.Ref(ChildAdmissionIntent.Schema));

        // Act
        var result = owner.Raise(Op(owner, id, nameof(RaiseFinding), null), Terms(), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(), initial);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, Pending(owner));
        Assert.Equal(2, owner.Revision);
        Assert.Equal(1, owner.AdmittedCount);
    }

    [Fact]
    public void ShouldRefuseRaiseGivenOperationOfAnotherFinding()
    {
        // Arrange
        var owner = Open(out _);

        // Act
        var result = owner.Raise(Op(owner, Uuid.CreateVersion4(), nameof(RaiseFinding), null), Terms(),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), null);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.False(owner.IsRecorded);
    }

    [Fact]
    public void ShouldRefuseRaiseGivenInitialActionOfAnotherStream()
    {
        // Arrange
        var owner = Open(out var id);
        var foreign = Open(out _);
        var initial = new InitialActionAdmission(Op(foreign, Uuid.CreateVersion4(), nameof(AddCorrectiveAction), null),
            NativeFacts.Ref(ChildAdmissionIntent.Schema));

        // Act
        var result = owner.Raise(Op(owner, id, nameof(RaiseFinding), null), Terms(), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(), initial);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.False(owner.IsRecorded);
    }

    [Fact]
    public void ShouldRefuseRaiseGivenFindingAlreadyRaised()
    {
        // Arrange
        var owner = Raised(out var id);

        // Act
        var result = owner.Raise(Op(owner, id, nameof(RaiseFinding), null), Terms(), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(), null);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, Pending(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseRevisionGivenFindingNeverRaised()
    {
        // Arrange
        var owner = Open(out _);

        // Act
        var result = owner.Revise(Op(owner, Uuid.CreateVersion4(), nameof(ReviseFinding), 0), Revision("low"),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(0, Pending(owner));
    }

    [Fact]
    public void ShouldReviseCurrentTermsGivenOpenFinding()
    {
        // Arrange
        var owner = Raised(out _);

        // Act
        var result = owner.Revise(Op(owner, Uuid.CreateVersion4(), nameof(ReviseFinding), 1), Revision("critical"),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, owner.Revision);
        Assert.Equal("critical", owner.Severity);
        Assert.Equal("Original source text.", owner.Raised!.Source.SourceText);
        Assert.Equal(2, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseSecondWriteGivenSameExpectedRevision()
    {
        // Arrange
        var owner = Raised(out _);
        Assert.True(owner.Revise(Op(owner, Uuid.CreateVersion4(), nameof(ReviseFinding), 1), Revision("critical"),
            NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);

        // Act
        var result = owner.Revise(Op(owner, Uuid.CreateVersion4(), nameof(ReviseFinding), 1), Revision("low"),
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("critical", owner.Severity);
        Assert.Equal(2, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseOrdinaryWriteGivenOperationWithoutExpectedRevision()
    {
        // Arrange
        var owner = Raised(out _);

        // Act
        var result = owner.LinkAcceptance(Op(owner, Uuid.CreateVersion4(), nameof(LinkFindingAcceptance), null),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(1, Pending(owner));
    }

    [Fact]
    public void ShouldLinkAcceptanceWithoutAdmittingRemediationGivenOpenFinding()
    {
        // Arrange
        var owner = Raised(out _);

        // Act
        var link = owner.LinkAcceptance(Op(owner, Uuid.CreateVersion4(), nameof(LinkFindingAcceptance), 1),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
        var close = owner.AcceptClosure(Op(owner, Uuid.CreateVersion4(), nameof(CloseFinding), owner.Revision),
            NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.True(link.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, close.Error?.Kind);
        Assert.Equal(0, owner.AdmittedCount);
        Assert.Equal(2, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseClosureGivenNoAdmittedAction()
    {
        // Arrange
        var owner = Raised(out _);

        // Act
        var result = owner.AcceptClosure(Op(owner, Uuid.CreateVersion4(), nameof(CloseFinding), 1),
            NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(owner.PendingClosure);
        Assert.Equal(1, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseClosureGivenOperationWhoseRootIsAnotherOperation()
    {
        // Arrange
        var action = Uuid.CreateVersion4();
        var owner = Raised(out _, action);
        Assert.True(owner.AdmitAction(Op(owner, action, nameof(AddCorrectiveAction), 1),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref()).IsSuccess);
        var derived = Op(owner, Uuid.CreateVersion4(), nameof(CloseFinding), owner.Revision) with
        {
            RootOperationId = Uuid.CreateVersion4(),
        };

        // Act
        var result = owner.AcceptClosure(derived, NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Null(owner.PendingClosure);
        Assert.Equal(2, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseActionIdentityReuseGivenActionAlreadyAdmitted()
    {
        // Arrange
        var action = Uuid.CreateVersion4();
        var owner = Raised(out _, action);
        Assert.True(owner.AdmitAction(Op(owner, action, nameof(AddCorrectiveAction), 1),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref()).IsSuccess);

        // Act
        var result = owner.AdmitAction(Op(owner, action, nameof(AddCorrectiveAction), owner.Revision),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, owner.AdmittedCount);
        Assert.Equal(2, Pending(owner));
    }

    [Fact]
    public void ShouldSealAdmissionAndWritesGivenPendingClosure()
    {
        // Arrange
        var action = Uuid.CreateVersion4();
        var owner = Raised(out _, action);
        Assert.True(owner.AdmitAction(Op(owner, action, nameof(AddCorrectiveAction), 1),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref()).IsSuccess);
        Assert.True(owner.AcceptClosure(Op(owner, Uuid.CreateVersion4(), nameof(CloseFinding), 2),
            NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref()).IsSuccess);

        // Act
        var revise = owner.Revise(Op(owner, Uuid.CreateVersion4(), nameof(ReviseFinding), owner.Revision),
            Revision("low"), NativeFacts.Ref(), NativeFacts.Ref());
        var secondClosure = owner.AcceptClosure(Op(owner, Uuid.CreateVersion4(), nameof(CloseFinding), owner.Revision),
            NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, revise.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, secondClosure.Error?.Kind);
        Assert.NotNull(owner.PendingClosure);
        Assert.Equal(3, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseCompletionGivenChangedClosureIntent()
    {
        // Arrange
        var owner = ClosurePending(out var closeOp, out _);

        // Act
        var result = owner.CompleteClosure(Child(closeOp, "CompleteFindingClosure"), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.NotNull(owner.PendingClosure);
        Assert.Null(owner.Closed);
        Assert.Equal(3, Pending(owner));
    }

    [Fact]
    public void ShouldCloseAndFreezeFindingGivenExactPendingClosure()
    {
        // Arrange
        var owner = ClosurePending(out var closeOp, out var intent);

        // Act
        var result = owner.CompleteClosure(Child(closeOp, "CompleteFindingClosure"), NativeFacts.Ref(),
            NativeFacts.Ref(), intent, NativeFacts.Ref());
        var revise = owner.Revise(Op(owner, Uuid.CreateVersion4(), nameof(ReviseFinding), owner.Revision),
            Revision("low"), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(owner.Closed);
        Assert.Null(owner.PendingClosure);
        Assert.Equal(1, owner.Closed!.FindingGeneration);
        Assert.Equal(RequestErrorKind.Conflict, revise.Error?.Kind);
        Assert.Equal(4, Pending(owner));
    }

    [Fact]
    public void ShouldRefuseCompletionAndCancelGivenNoPendingClosure()
    {
        // Arrange
        var owner = Raised(out _);
        var stray = Op(owner, Uuid.CreateVersion4(), nameof(CloseFinding), 1);

        // Act
        var complete = owner.CompleteClosure(stray, NativeFacts.Ref(), NativeFacts.Ref(),
            NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref());
        var cancel = owner.CancelClosure(stray, NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, complete.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, cancel.Error?.Kind);
        Assert.Equal(1, Pending(owner));
    }

    [Fact]
    public void ShouldReopenAtNextGenerationAndAdmitActionGivenClosedFinding()
    {
        // Arrange
        var owner = ClosurePending(out var closeOp, out var intent, out var secondAction);
        Assert.True(owner.CompleteClosure(Child(closeOp, "CompleteFindingClosure"), NativeFacts.Ref(),
            NativeFacts.Ref(), intent, NativeFacts.Ref()).IsSuccess);

        // Act
        var reopen = owner.Reopen(Op(owner, Uuid.CreateVersion4(), nameof(ReopenFinding), owner.Revision),
            "Access regained.", Lead, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());
        var admit = owner.AdmitAction(Op(owner, secondAction, nameof(AddCorrectiveAction), owner.Revision),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.True(reopen.IsSuccess);
        Assert.True(admit.IsSuccess);
        Assert.Equal(2, owner.Generation);
        Assert.Null(owner.Closed);
        Assert.Equal(2, owner.AdmittedCount);
        var admitted = Assert.IsType<CorrectiveActionAdmitted>(
            new AggregateScenario<FindingAggregate>(owner).PendingEvents[^1]);
        Assert.Equal(2, admitted.Generation);
    }

    [Fact]
    public void ShouldRefuseReopenGivenOpenFindingOrStaleRevision()
    {
        // Arrange
        var open = Raised(out _);
        var closed = ClosurePending(out var closeOp, out var intent);
        Assert.True(closed.CompleteClosure(Child(closeOp, "CompleteFindingClosure"), NativeFacts.Ref(),
            NativeFacts.Ref(), intent, NativeFacts.Ref()).IsSuccess);

        // Act
        var onOpen = open.Reopen(Op(open, Uuid.CreateVersion4(), nameof(ReopenFinding), 1), "No.", Lead,
            NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());
        var stale = closed.Reopen(Op(closed, Uuid.CreateVersion4(), nameof(ReopenFinding), closed.Revision - 1),
            "No.", Lead, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, onOpen.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.NotNull(closed.Closed);
        Assert.Equal(1, closed.Generation);
        Assert.Equal(1, Pending(open));
        Assert.Equal(4, Pending(closed));
    }

    [Fact]
    public void ShouldRehydrateAndResolveCommittedFactsGivenRetryAfterLaterFacts()
    {
        // Arrange
        var action = Uuid.CreateVersion4();
        var closureRoot = Uuid.CreateVersion4();
        var source = Raised(out var id, action);
        Assert.True(source.AdmitAction(Op(source, action, nameof(AddCorrectiveAction), 1),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref()).IsSuccess);
        Assert.True(source.Revise(Op(source, Uuid.CreateVersion4(), nameof(ReviseFinding), 2), Revision("critical"),
            NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        var closeOp = Op(source, closureRoot, nameof(CloseFinding), 3);
        var intent = NativeFacts.Ref(ClosureIntent.Schema);
        Assert.True(source.AcceptClosure(closeOp, intent, NativeFacts.Ref()).IsSuccess);
        Assert.True(source.CompleteClosure(Child(closeOp, "CompleteFindingClosure"), NativeFacts.Ref(),
            NativeFacts.Ref(), intent, NativeFacts.Ref()).IsSuccess);
        Assert.True(source.Reopen(Op(source, Uuid.CreateVersion4(), nameof(ReopenFinding), source.Revision),
            "Access regained.", Lead, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        var facts = NativeFacts.Raised(source);

        // Act
        var owner = NativeFacts.Replay(new FindingAggregate(source.TenantId, source.ProgramId, id, action, closureRoot),
            facts);
        var admitted = owner.HasAdmitted(action);
        var terminal = owner.TerminalClosure(closureRoot);
        var accepted = owner.AcceptedClosure(closureRoot);
        var retry = owner.AdmitAction(Op(owner, action, nameof(AddCorrectiveAction), owner.Revision),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.Equal(6, owner.Revision);
        Assert.Equal(2, owner.Generation);
        Assert.Equal("critical", owner.Severity);
        Assert.Null(owner.Closed);
        Assert.Null(owner.PendingClosure);
        Assert.True(admitted);
        Assert.IsType<FindingClosedV2>(terminal);
        Assert.Equal(intent, accepted!.Intent);
        Assert.Equal(RequestErrorKind.Conflict, retry.Error?.Kind);
        Assert.Equal(0, Pending(owner));
        Assert.Equal((ulong)facts.Length, owner.Version);
    }

    static int Pending(FindingAggregate owner) => new AggregateScenario<FindingAggregate>(owner).PendingEvents.Count;

    static FindingAggregate Open(out Uuid id, Uuid selectedAction = default, Uuid selectedClosureRoot = default)
    {
        id = Uuid.CreateVersion4();
        return new FindingAggregate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), id, selectedAction,
            selectedClosureRoot);
    }

    static FindingAggregate Raised(out Uuid id, Uuid selectedAction = default)
    {
        var owner = Open(out id, selectedAction);
        Assert.True(owner.Raise(Op(owner, id, nameof(RaiseFinding), null), Terms(), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(), null).IsSuccess);
        return owner;
    }

    static FindingAggregate ClosurePending(out OperationProvenance closeOp, out ImmutableContentRef intent) =>
        ClosurePending(out closeOp, out intent, out _);

    static FindingAggregate ClosurePending(out OperationProvenance closeOp, out ImmutableContentRef intent,
        out Uuid secondAction)
    {
        secondAction = Uuid.CreateVersion4();
        var owner = Open(out var id, secondAction);
        var initial = new InitialActionAdmission(Op(owner, Uuid.CreateVersion4(), nameof(AddCorrectiveAction), null),
            NativeFacts.Ref(ChildAdmissionIntent.Schema));
        Assert.True(owner.Raise(Op(owner, id, nameof(RaiseFinding), null), Terms(), NativeFacts.Ref(),
            NativeFacts.Ref(), NativeFacts.Ref(), initial).IsSuccess);
        closeOp = Op(owner, Uuid.CreateVersion4(), nameof(CloseFinding), 2);
        intent = NativeFacts.Ref(ClosureIntent.Schema);
        Assert.True(owner.AcceptClosure(closeOp, intent, NativeFacts.Ref()).IsSuccess);
        return owner;
    }

    static OperationProvenance Op(FindingAggregate owner, Uuid logicalId, string kind, long? expected) =>
        FindingContent.Provenance(logicalId, kind, owner.Stream, Lead, expected is null ? null : (ulong)expected.Value,
            new string('a', 64), NativeFacts.Now) with
        { FirstOwner = owner.Stream };

    static OperationProvenance Child(OperationProvenance root, string kind) => root with
    {
        LogicalId = Uuid.CreateVersion4(),
        CommandKind = kind,
    };

    static FindingRaiseTerms Terms() => new(new FindingSource("manual", null, null, "Original source text."),
        "Retained access", "Access survived offboarding.", "high", "VPN", Uuid.CreateVersion4(),
        new DateOnly(2026, 10, 15), Lead, NativeFacts.Now, "manual", null);

    static FindingRevision Revision(string severity) => new(severity, Uuid.CreateVersion4(),
        new DateOnly(2026, 10, 20), "VPN and SSO", null, "Triage.", Lead, NativeFacts.Now);
}
