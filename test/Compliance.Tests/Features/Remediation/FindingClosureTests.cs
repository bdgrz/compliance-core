using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingClosureTests
{
    [Fact]
    public async Task ShouldKeepClosedParentFenceGivenChildSourceUseSettled()
    {
        // Arrange: finding F generation 1 is sealed with completed action A at public revision 2.
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var actionId = finding.CorrectiveActions[0].ActionId;
        var close = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, close);
        var record = await fixture.StartClosureAsync(finding.FindingId, close.CommandId);
        var target = Assert.Single(record.Targets);
        var untouched = await fixture.ActionAsync(finding.FindingId, actionId);

        // Act: step 1, closure use is authorized on the exact completion event.
        var acquired = await fixture.SendReceiverAsync(await fixture.ClosureStepAsync(record, "acquire",
            nameof(AuthorizeCorrectiveActionFindingClosureUse), target));
        var held = await fixture.ActionAsync(finding.FindingId, actionId);
        var blocked = await fixture.Scenario(fixture.OwnerUserId).When(fixture.Complete(finding, actionId, "Edited.",
            OperationsFixture.FullSupport)).ExpectFailure(RequestErrorKind.Conflict);

        // Assert: the physical version advances, the public revision stays 2 and edits are blocked.
        Assert.IsType<ReceiverOutcome.Applied>(acquired.Value);
        Assert.Equal(2, held.Revision);
        Assert.Equal(untouched.Version + 1, held.Version);
        Assert.True(held.Uses.HasActiveUse);
        Assert.Contains("holds this corrective action", blocked.Error!.Message, StringComparison.Ordinal);

        // Act: step 2, FindingClosedV2 is appended first, then the exact use settles as parent_closed.
        var completeCommand = await fixture.ClosureStepAsync(record, "complete", nameof(CompleteFindingClosure), null);
        var completed = await fixture.SendReceiverAsync(completeCommand);
        var closedFact = Assert.IsType<ReceiverOutcome.Applied>(completed.Value).Fact;
        var stillHeld = await fixture.ActionAsync(finding.FindingId, actionId);
        var settleRecord = record with
        {
            Facts = new Dictionary<string, NativeFactReference> { [nameof(CompleteFindingClosure)] = closedFact },
        };
        var settled = await fixture.SendReceiverAsync(await fixture.ClosureStepAsync(settleRecord, "settle",
            nameof(SettleCorrectiveActionFindingClosureUse), target));
        var fenced = await fixture.ActionAsync(finding.FindingId, actionId);

        // Assert: the parent fence is installed before the temporary use is released and the revision never moved.
        Assert.True(stillHeld.Uses.HasActiveUse);
        Assert.IsType<ReceiverOutcome.Applied>(settled.Value);
        Assert.False(fenced.Uses.HasActiveUse);
        Assert.Equal([1L], fenced.ClosedGenerations);
        Assert.Equal(2, fenced.Revision);

        // Act: step 3, an edit at the same public revision is rejected under the parent fence.
        var edit = await fixture.Scenario(fixture.OwnerUserId).When(fixture.Complete(finding, actionId, "Edited again.",
            OperationsFixture.FullSupport)).ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Contains("fenced by a closed finding generation", edit.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(fenced.Version, (await fixture.ActionAsync(finding.FindingId, actionId)).Version);

        // Act: step 4, the finding reopens to the next generation and the old delivery arrives late.
        var closed = await fixture.GetFindingAsync(finding.FindingId);
        var reopened = await fixture.AsAsync(fixture.LeadUserId, new ReopenFinding(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, closed.Revision, "A leaver regained access."));
        var findingVersion = (await fixture.FindingStreamAsync(finding.FindingId)).Version;
        var acquire = (AuthorizeCorrectiveActionFindingClosureUse)await fixture.ClosureStepAsync(record, "acquire",
            nameof(AuthorizeCorrectiveActionFindingClosureUse), target);
        var oldAcquire = await fixture.SendReceiverAsync(acquire);
        var lateAcquire = await fixture.SendReceiverAsync(acquire with { CommandId = Uuid.CreateVersion4() });
        var oldComplete = await fixture.SendReceiverAsync(completeCommand);
        var retry = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, close);
        var after = await fixture.FindingStreamAsync(finding.FindingId);
        var action = await fixture.ActionAsync(finding.FindingId, actionId);

        // Assert: nothing closes or clears authority for the new generation.
        Assert.Equal("remediated", reopened.Status);
        Assert.IsType<ReceiverOutcome.Superseded>(oldAcquire.Value);
        Assert.IsType<ReceiverOutcome.Superseded>(lateAcquire.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(oldComplete.Value);
        Assert.True(retry.IsSuccess);
        Assert.Equal(findingVersion, after.Version);
        Assert.Equal(2, after.Generation);
        Assert.Null(after.Closed);
        Assert.Null(after.PendingClosure);
        Assert.False(action.Uses.HasActiveUse);
        Assert.Equal([1L], action.ClosedGenerations);
        Assert.Equal(fenced.Version, action.Version);
    }

    [Fact]
    public async Task ShouldBlockClosureGivenAcceptedButUnmaterializedAction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(5), "Second.",
            materialize: false);
        var second = finding.CorrectiveActions.Single(item => item.Revision == 0);
        var close = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, close);

        // Act
        var waiting = await fixture.RunClosureAsync(finding.FindingId, close.CommandId);
        var pending = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(ProcessState.Pending, waiting.State);
        Assert.Contains("not materialized yet", waiting.Note, StringComparison.Ordinal);
        Assert.NotNull(pending.PendingClosure);
        Assert.Null(pending.Closed);
        Assert.Null(pending.TerminalClosure(close.CommandId));

        // Act: the admitted action materializes and completes, then the same closure resumes.
        await fixture.MaterializeAsync(finding.FindingId);
        finding = await fixture.GetFindingAsync(finding.FindingId);
        await fixture.CompleteActionAsync(fixture.OwnerUserId, finding, second.ActionId);
        var resumed = await fixture.RunClosureAsync(finding.FindingId, close.CommandId);
        var closed = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal("closed", closed.Status);
        Assert.Equal(2, closed.CorrectiveActions.Count(item => item.Status == "completed"));
    }

    [Fact]
    public async Task ShouldCancelClosureAndSettleEveryUseGivenUnfinishedAction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(5), "Second.");
        var close = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, close);

        // Act
        var ended = await fixture.RunClosureAsync(finding.FindingId, close.CommandId);
        var canceled = await fixture.FindingStreamAsync(finding.FindingId);
        var actions = new List<CorrectiveAction>();
        foreach (var action in finding.CorrectiveActions)
            actions.Add(await fixture.ActionAsync(finding.FindingId, action.ActionId));

        // Assert
        Assert.Equal(ProcessState.Completed, ended.State);
        Assert.IsType<FindingClosureCanceled>(canceled.TerminalClosure(close.CommandId));
        Assert.Null(canceled.PendingClosure);
        Assert.Null(canceled.Closed);
        Assert.All(actions, action =>
        {
            Assert.False(action.Uses.HasActiveUse);
            Assert.Empty(action.ClosedGenerations);
        });

        // Act: the action is completed and a new closure of the same generation closes the finding.
        finding = await fixture.GetFindingAsync(finding.FindingId);
        finding = await fixture.CompleteActionAsync(fixture.OwnerUserId, finding,
            finding.CorrectiveActions.Single(item => item.Status == "open").ActionId);
        var closedAgain = await fixture.CloseAsync(fixture.ApproverUserId, finding);

        // Assert
        Assert.Equal("closed", closedAgain.Status);
    }

    [Fact]
    public async Task ShouldRejectActionAdmissionGivenClosureSealed()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId,
            fixture.Close(finding));
        var sealedFinding = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        var late = await fixture.Scenario(fixture.LeadUserId).When(new AddCorrectiveAction(Uuid.CreateVersion4(),
                fixture.TenantId, fixture.ProgramId, finding.FindingId, sealedFinding.Revision, "Late work.",
                fixture.OwnerMemberId, fixture.Today.AddDays(3)))
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Contains("closure of this finding is in progress", late.Error!.Message, StringComparison.Ordinal);
        Assert.Single((await fixture.FindingStreamAsync(finding.FindingId)).Admitted);
    }

    [Fact]
    public async Task ShouldAdmitMoreActionsThanFormerCapGivenNoArbitraryLimit()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);

        // Act
        for (var index = 0; index < 52; index++)
            finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(5),
                $"Action {index}.", materialize: false);
        await fixture.MaterializeAsync(finding.FindingId);
        finding = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        Assert.Equal(52, finding.CorrectiveActions.Count);
        Assert.All(finding.CorrectiveActions, action => Assert.Equal(1, action.Revision));
        Assert.Equal(53, finding.Revision);
    }

    [Fact]
    public async Task ShouldCloseGivenExactWaiverProofForInvolvedCloser()
    {
        // Arrange: the lead owns the finding, so only an exact waiver for this finding revision can authorize the closure.
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture, fixture.LeadMemberId);
        var waiverId = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Finding, finding.FindingId, finding.FindingId, finding.Revision,
            SeparationOfDutiesActions.Approve), fixture.LeadMemberId);

        // Act
        var closed = await fixture.CloseAsync(fixture.LeadUserId, finding, waiverId);

        // Assert
        Assert.Equal("closed", closed.Status);
        Assert.Equal(waiverId, closed.Closure!.SeparationOfDutiesWaiverId);
        Assert.Equal(fixture.LeadMemberId, closed.Closure.CloserMemberId);
    }

    [Fact]
    public async Task ShouldRejectClosureGivenWaiverForAnotherFindingRevisionOrActor()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture, fixture.LeadMemberId);
        var wrongRevision = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Finding, finding.FindingId, finding.FindingId, finding.Revision + 1,
            SeparationOfDutiesActions.Approve), fixture.LeadMemberId);
        var wrongActor = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Finding, finding.FindingId, finding.FindingId, finding.Revision,
            SeparationOfDutiesActions.Approve), fixture.OwnerMemberId);
        var wrongRecord = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Finding, Uuid.CreateVersion4(), Uuid.CreateVersion4(), finding.Revision,
            SeparationOfDutiesActions.Approve), fixture.LeadMemberId);

        // Act
        var results = new List<Result<OperationAcceptedResponse>>();
        foreach (var waiver in new[] { wrongRevision, wrongActor, wrongRecord })
            results.Add(await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.LeadUserId,
                fixture.Close(finding, waiver), RequestErrorKind.Forbidden));
        var none = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.LeadUserId,
            fixture.Close(finding), RequestErrorKind.Forbidden);

        // Assert
        Assert.All(results, result => Assert.Contains("waiver is not active", result.Error!.Message,
            StringComparison.Ordinal));
        Assert.Contains("cannot verify and close their own remediation", none.Error!.Message, StringComparison.Ordinal);
        Assert.Null((await fixture.FindingStreamAsync(finding.FindingId)).PendingClosure);
    }

    [Fact]
    public async Task ShouldCancelClosureGivenCompleterIsCloserWithoutWaiverProof()
    {
        // Arrange: a program manager who is neither owner nor admitted-action owner completed the work and then closes it.
        var fixture = await OperationsFixture.CreateAsync();
        var raised = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var finding = await fixture.AddActionAsync(raised, fixture.OwnerMemberId, fixture.Today.AddDays(5));
        finding = await fixture.CompleteActionAsync(fixture.ApproverUserId, finding, finding.CorrectiveActions[0].ActionId);
        var close = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, close);

        // Act
        var ended = await fixture.RunClosureAsync(finding.FindingId, close.CommandId);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(ProcessState.Completed, ended.State);
        Assert.IsType<FindingClosureCanceled>(after.TerminalClosure(close.CommandId));
        Assert.Null(after.Closed);
    }

    [Fact]
    public async Task ShouldResumeClosureWithoutDuplicateFactsGivenCrashAfterSomeSteps()
    {
        // Arrange
        var crash = new CrashingProcessStore();
        var fixture = await OperationsFixture.CreateAsync(crash.Wrap);
        var raised = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var finding = raised;
        for (var index = 0; index < 3; index++)
        {
            finding = await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(5), $"Action {index}.");
            finding = await fixture.CompleteActionAsync(fixture.OwnerUserId, finding,
                finding.CorrectiveActions.Single(item => item.Status == "open").ActionId);
        }

        var close = fixture.Close(finding);
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, close);
        var record = await fixture.StartClosureAsync(finding.FindingId, close.CommandId);
        crash.ArmAfterReplaceCount(5);

        // Act
        var crashed = await Record.ExceptionAsync(() => fixture.RunClosureAsync(finding.FindingId, close.CommandId));
        var interrupted = await fixture.Provider.GetRequiredService<IProcessRecordStore>().ReadAsync(record.Key);
        crash.Disarm();
        var resumed = await fixture.RunClosureAsync(finding.FindingId, close.CommandId);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.IsType<InvalidOperationException>(crashed);
        Assert.NotEqual(ProcessState.Completed, interrupted!.State);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Single(after.ClosureHistory);
        foreach (var action in finding.CorrectiveActions)
        {
            var state = await fixture.ActionAsync(finding.FindingId, action.ActionId);
            Assert.Equal(1UL, state.Uses.Generation);
            Assert.False(state.Uses.HasActiveUse);
            Assert.Equal([1L], state.ClosedGenerations);
        }
    }

    [Fact]
    public async Task ShouldNotDuplicateMaterializationGivenReceiverAppendBeforeCheckpointCrash()
    {
        // Arrange
        var crash = new CrashingProcessStore();
        var fixture = await OperationsFixture.CreateAsync(crash.Wrap);
        var raised = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var finding = await fixture.AddActionAsync(raised, fixture.OwnerMemberId, fixture.Today.AddDays(5), "Fix.",
            materialize: false);
        var actionId = finding.CorrectiveActions[0].ActionId;
        // Three skipped receivers take two checkpoints each before the corrective action receiver runs.
        crash.ArmAfterReplaceCount(7);

        // Act
        var crashed = await Record.ExceptionAsync(() => fixture.MaterializeAsync(finding.FindingId));
        var appended = await fixture.ActionAsync(finding.FindingId, actionId);
        crash.Disarm();
        await fixture.MaterializeAsync(finding.FindingId);
        var action = await fixture.ActionAsync(finding.FindingId, actionId);

        // Assert
        Assert.Equal("Worker crashed before the CAS.", crashed!.Message);
        Assert.True(appended.IsMaterialized);
        Assert.Equal(appended.Version, action.Version);
        Assert.Equal(1, action.Revision);
    }
}
