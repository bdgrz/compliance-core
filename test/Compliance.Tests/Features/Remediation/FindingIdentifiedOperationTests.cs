using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

/// <summary>Every finding command is one immutable authored operation: an exact retry replays, a changed one conflicts.</summary>
public sealed class FindingIdentifiedOperationTests
{
    [Fact]
    public async Task ShouldReplayRegistrationAndConflictGivenRaiseRetryWithChangedIntent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var raise = fixture.Raise(fixture.OwnerMemberId);
        var first = await fixture.AsAsync(fixture.LeadUserId, raise);
        var version = (await fixture.FindingStreamAsync(raise.FindingId)).Version;

        // Act
        var retry = await fixture.AsAsync(fixture.LeadUserId, raise);
        var changed = await fixture.Scenario(fixture.LeadUserId).When(raise with { Title = "A different finding" })
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(first, retry);
        Assert.Equal(1, retry.Revision);
        Assert.Equal(version, (await fixture.FindingStreamAsync(raise.FindingId)).Version);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
    }

    [Fact]
    public async Task ShouldReplayAcknowledgmentAndConflictGivenAdmissionRetryAfterLaterChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var add = new AddCorrectiveAction(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, finding.FindingId, 1,
            "Remove stale access.", fixture.OwnerMemberId, fixture.Today.AddDays(5));
        var first = await fixture.AsAsync(fixture.LeadUserId, add);
        await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            finding.FindingId, 2, "low", fixture.OwnerMemberId, fixture.Today.AddDays(9), "All", null, "Triage."));
        var version = (await fixture.FindingStreamAsync(finding.FindingId)).Version;

        // Act
        var retry = await fixture.AsAsync(fixture.LeadUserId, add);
        var changedOwner = await fixture.Scenario(fixture.LeadUserId)
            .When(add with { OwnerMemberId = fixture.BackupMemberId }).ExpectFailure(RequestErrorKind.Conflict);
        var changedRevision = await fixture.Scenario(fixture.LeadUserId)
            .When(add with { ExpectedFindingRevision = 2 }).ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(first, retry);
        Assert.Equal(add.ActionId, retry.OperationId);
        Assert.Equal(version, (await fixture.FindingStreamAsync(finding.FindingId)).Version);
        Assert.Equal(RequestErrorKind.Conflict, changedOwner.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedRevision.Error!.Kind);
    }

    [Fact]
    public async Task ShouldReplayOriginalViewAndConflictGivenCompletionRetryAfterLaterChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var raised = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var finding = await fixture.AddActionAsync(raised, fixture.OwnerMemberId, fixture.Today.AddDays(5));
        var actionId = finding.CorrectiveActions[0].ActionId;
        var complete = fixture.Complete(finding, actionId, "Removed.", OperationsFixture.FullSupport);
        var first = await fixture.AsAsync(fixture.OwnerUserId, complete);
        await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            finding.FindingId, finding.Revision, "low", fixture.OwnerMemberId, fixture.Today.AddDays(9), "All", null,
            "Triage."));
        var version = (await fixture.ActionAsync(finding.FindingId, actionId)).Version;

        // Act
        var retry = await fixture.AsAsync(fixture.OwnerUserId, complete);
        var changed = await fixture.Scenario(fixture.OwnerUserId).When(complete with { ResolutionNotes = "Other." })
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(WireJson.Of(first), WireJson.Of(retry));
        Assert.Equal(2, retry.CorrectiveActions[0].Revision);
        Assert.Equal("high", retry.Severity);
        Assert.Equal(version, (await fixture.ActionAsync(finding.FindingId, actionId)).Version);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
    }

    [Fact]
    public async Task ShouldReplayOriginalViewAndConflictGivenAcceptanceLinkRetry()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var waiverId = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Finding, finding.FindingId, finding.FindingId, 1,
            SeparationOfDutiesActions.Approve), fixture.LeadMemberId);
        var link = new LinkFindingAcceptance(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, finding.FindingId,
            1, "waiver", waiverId);
        var first = await fixture.AsAsync(fixture.LeadUserId, link);
        var version = (await fixture.FindingStreamAsync(finding.FindingId)).Version;

        // Act
        var retry = await fixture.AsAsync(fixture.LeadUserId, link);
        var changed = await fixture.Scenario(fixture.LeadUserId).When(link with { RecordId = Uuid.CreateVersion4() })
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(WireJson.Of(first), WireJson.Of(retry));
        Assert.Equal(version, (await fixture.FindingStreamAsync(finding.FindingId)).Version);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
    }

    [Fact]
    public async Task ShouldReplayAcknowledgmentAndConflictGivenClosureRetryAfterTheFindingClosed()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var close = fixture.Close(finding);
        var first = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, close);
        await fixture.RunClosureAsync(finding.FindingId, close.CommandId);
        var version = (await fixture.FindingStreamAsync(finding.FindingId)).Version;

        // Act
        var retry = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, close);
        var changed = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId,
            close with { Rationale = "A different rationale." }, RequestErrorKind.Conflict);
        var actor = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.LeadUserId, close,
            RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(close.CommandId, retry.Value.OperationId);
        Assert.Equal(version, (await fixture.FindingStreamAsync(finding.FindingId)).Version);
        Assert.False(changed.IsSuccess);
        Assert.False(actor.IsSuccess);
    }

    [Fact]
    public async Task ShouldReplayOriginalViewAndConflictGivenReopeningRetry()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await FindingTests.RaiseWithCompletedActionAsync(fixture);
        var closed = await fixture.CloseAsync(fixture.ApproverUserId, finding);
        var reopen = new ReopenFinding(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, finding.FindingId,
            closed.Revision, "A leaver regained access.");
        var first = await fixture.AsAsync(fixture.LeadUserId, reopen);
        var version = (await fixture.FindingStreamAsync(finding.FindingId)).Version;

        // Act
        var retry = await fixture.AsAsync(fixture.LeadUserId, reopen);
        var changed = await fixture.Scenario(fixture.LeadUserId).When(reopen with { Reason = "Another reason." })
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(WireJson.Of(first), WireJson.Of(retry));
        Assert.Equal(version, (await fixture.FindingStreamAsync(finding.FindingId)).Version);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRejectReceiverGivenNonSystemPrincipal()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var stream = new FindingAggregate(fixture.TenantId, fixture.ProgramId, finding.FindingId).Stream;
        var abort = new AbortFindingClosure(Uuid.CreateVersion4(), Uuid.CreateVersion4(), stream, 1, 0,
            new Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef(Uuid.CreateVersion4(), new string("0"[0], 64), 2,
                "application/json", "failure.v1"));

        // Act
        var member = await fixture.Scenario(fixture.LeadUserId).When(abort).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, member.Error!.Kind);
    }
}
