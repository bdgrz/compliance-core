using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Risks.History;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskOwnerBoundaryTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid RiskId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly ActorReference Proposer = ActorReference.ForMember(ProposerId, "Proposer");
    static readonly ActorReference Reviewer = ActorReference.ForMember(ReviewerId, "Reviewer");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ImmutableContentRef Content = new(Uuid.CreateVersion4(), new string('a', 64), 1,
        "application/json", "risk-owner-test.v1");

    [Fact]
    public void ShouldAcceptControlTreatmentGivenIndependentReviewer()
    {
        // Arrange
        var line = NewLine();
        var treatmentId = Propose(line, 0);

        // Act
        var result = line.Review(Operation(line, Uuid.CreateVersion4(), 1, Reviewer), treatmentId, "accept",
            "The control addresses the scenario.", Reviewer, Now.AddMinutes(1), null, Content, Content);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(treatmentId, line.Accepted!.TreatmentId);
        Assert.Equal("accepted", line.Find(treatmentId)!.Status);
        Assert.Equal(Reviewer, line.Find(treatmentId)!.Reviewed!.Reviewer);
        Assert.Equal(2, line.Revision);
        Assert.Single(line.Treatments);
    }

    [Fact]
    public async Task ShouldRejectSelfReviewGivenProposerWithoutExactWaiver()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var proposed = await fixture.Scenario(fixture.AssessorUserId).When(Proposal(fixture)).ExpectSuccess();
        var wrong = await TreatmentWaiverAsync(fixture, proposed.Value.TreatmentId, 2);
        var exact = await TreatmentWaiverAsync(fixture, proposed.Value.TreatmentId, 1);
        var review = fixture.ReviewTreatment(proposed.Value.TreatmentId, "accept");

        // Act
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId, review,
            RequestErrorKind.Forbidden);
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            review with { CommandId = Uuid.CreateVersion4(), SeparationOfDutiesWaiverId = wrong },
            RequestErrorKind.Forbidden);
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            review with { CommandId = Uuid.CreateVersion4(), SeparationOfDutiesWaiverId = exact });

        // Assert
        Assert.Equal(exact, Assert.Single((await fixture.GovernanceAsync()).ControlTreatments).SeparationOfDutiesWaiverId);
    }

    [Fact]
    public async Task ShouldRejectProposalGivenNonMitigateTreatmentStaleRevisionOrPendingDuplicate()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var provider = fixture.Provider;
        var line = NewLine();
        _ = Propose(line, 0);

        // Act
        await fixture.Scenario(fixture.AssessorUserId).When(Proposal(fixture)).ExpectFailure(RequestErrorKind.Conflict);
        var stale = line.PreparePropose(0);
        var pending = line.PreparePropose(1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, pending.Error?.Kind);
        Assert.Equal(1, line.Revision);
        Assert.Single(line.Facts);
    }

    [Fact]
    public void ShouldSupersedeEarlierAssertionGivenAcceptedSuccessorTreatment()
    {
        // Arrange
        var line = NewLine();
        var first = Propose(line, 0);
        Assert.True(line.Review(Operation(line, Uuid.CreateVersion4(), 1, Reviewer), first, "accept", "Reviewed",
            Reviewer, Now, null, Content, Content).IsSuccess);
        var second = Propose(line, 2);

        // Act
        var reviewed = line.Review(Operation(line, Uuid.CreateVersion4(), 3, Reviewer), second, "accept", "Successor",
            Reviewer, Now, null, Content, Content);
        var retired = line.Retire(Operation(line, Uuid.CreateVersion4(), 4, Proposer), second, "Control retired.",
            Proposer, Now.AddDays(1), Content, Content);

        // Assert
        Assert.True(reviewed.IsSuccess, reviewed.Error?.Message);
        Assert.True(retired.IsSuccess, retired.Error?.Message);
        Assert.Equal("superseded", line.Find(first)!.Status);
        Assert.Equal("retired", line.Find(second)!.Status);
        Assert.Null(line.Accepted);
        Assert.Equal(5, line.Revision);
    }

    [Fact]
    public async Task ShouldRaiseTriggerOnceAndResolveGivenLaterInherentAssessment()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var provider = fixture.Provider;
        var at = DateTimeOffset.UtcNow;
        var reference = Uuid.CreateVersion4().ToString();
        var request = new RaiseRiskReassessmentTriggers(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            "method_changed", reference, at, Uuid.CreateVersion4(), fixture.RiskId);

        // Act
        await RequestScenario.For(provider).GivenActor(RequestActor.System).When(request).ExpectSuccess();
        await RequestScenario.For(provider).GivenActor(RequestActor.System)
            .When(request with { CommandId = Uuid.CreateVersion4(), RaisedAt = at.AddMinutes(5) }).ExpectSuccess();
        await using var scope = provider.CreateAsyncScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>()
            .ReadRiskAsync(fixture.TenantId, fixture.ProgramId, fixture.RiskId);
        var open = snapshot.OpenTriggers(fixture.RiskId, [Assessment(at.AddMinutes(-1))]);
        var resolved = snapshot.OpenTriggers(fixture.RiskId, [Assessment(at.AddMinutes(1))]);

        // Assert
        var trigger = Assert.Single(open);
        Assert.Equal(RiskGovernance.TriggerIdFor(fixture.ProgramId, fixture.RiskId, "method_changed", reference),
            trigger.TriggerId);
        Assert.Empty(resolved);
        Assert.Equal(1, snapshot.RevisionOf(fixture.RiskId));
        Assert.Equal("resolved", Assert.Single(snapshot.View(fixture.RiskId, [Assessment(at.AddMinutes(1))],
            DateOnly.FromDateTime(at.UtcDateTime)).ReassessmentTriggers).Status);
    }

    [Fact]
    public async Task ShouldRecordPersonOwnerGivenCorrelatedMember()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var provider = fixture.Provider;
        var personId = await fixture.SeedPersonAsync(fixture.ApproverUserId);
        var request = new AssignRiskOwner(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.RiskId,
            0, personId, "Owns the service.");

        // Act
        await fixture.Scenario(fixture.AssessorUserId).When(request).ExpectSuccess();
        await fixture.Scenario(fixture.AssessorUserId).When(request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 1
        }).ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AssessorUserId).When(request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 1,
            PersonId = Uuid.Empty
        }).ExpectFailure(RequestErrorKind.Validation);
        var owner = await ProgramManagementServices.HydrateAsync(provider,
            new RiskGovernance(fixture.TenantId, fixture.ProgramId, fixture.RiskId));

        // Assert
        var view = (await fixture.GovernanceAsync()).Owner!;
        Assert.Equal(personId, view.PersonId);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.ApproverUserId), view.CorrelatedMemberId);
        await using var ownerScope = provider.CreateAsyncScope();
        Assert.IsType<RiskOwnerAssigned>(Assert.Single(await ownerScope.ServiceProvider.GetRequiredService<RiskGovernanceReader>()
            .ReadGovernanceFactsAsync(owner)));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public async Task ShouldKeepActionIncompleteGivenSubmissionUntilIndependentReviewAccepts()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(false, worker);
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [evidence], 0)).ExpectSuccess();
        var submit = fixture.Submit(added.Value.ActionId, 1, [evidence]);

        // Act
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            submit with { CommandId = Uuid.CreateVersion4(), EvidenceRequestIds = [] }, RequestErrorKind.Validation);
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId, submit,
            RequestErrorKind.Conflict);
        await fixture.FulfilEvidenceAsync(evidence);
        var submitted = await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId, submit);
        var pending = await fixture.GovernanceAsync();
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.ReviewAction(added.Value.ActionId, 2, "accept"), RequestErrorKind.Forbidden);
        await fixture.AssignReviewAsync(submitted.Value.SubmissionId);
        var review = fixture.ReviewAction(added.Value.ActionId, 2, "accept");
        await using var scope = provider.CreateAsyncScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(review,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.ApproverUserId),
                new HttpInvocation("POST", "/synthetic/review", "/synthetic/review", "synthetic")), CancellationToken.None);
        var admitted = await fixture.ActionAsync(added.Value.ActionId);
        var process = await RiskActionProcesses.RunAsync(provider, fixture.TenantId, review.CommandId);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal("completion_submitted", Assert.Single(pending.TreatmentActions).Status);
        Assert.Equal("in_progress", pending.TreatmentActionStatus);
        Assert.Equal("completion_submitted", admitted.Status);
        Assert.NotNull(admitted.PendingReview);
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal("completed", (await fixture.ActionAsync(added.Value.ActionId)).Status);
        Assert.Equal("completed", (await fixture.GovernanceAsync()).TreatmentActionStatus);
        var request = await ProgramManagementServices.HydrateAsync(provider,
            new EvidenceRequest(fixture.TenantId, fixture.ProgramId, evidence));
        Assert.False(request.Uses.HasActiveUse);
        Assert.Equal(2, request.Revision);
    }

    [Fact]
    public async Task ShouldReviseOpenTreatmentActionGivenValidEdit()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var revisedOwner = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(false, revisedOwner);
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [], 0)).ExpectSuccess();
        var edit = new ReviseRiskTreatmentAction(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, added.Value.ActionId, 1, "Phishing-resistant MFA", "Hardware keys required", "IdP export",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40), revisedOwner.MemberId, [evidence]);

        // Act
        await fixture.Scenario(fixture.AssessorUserId).When(edit).ExpectSuccess();
        var view = Assert.Single((await fixture.GovernanceAsync()).TreatmentActions);
        var action = await fixture.ActionAsync(added.Value.ActionId);

        // Assert
        Assert.Equal(edit.Title, view.Title);
        Assert.Equal(edit.TargetState, view.TargetState);
        Assert.Equal(edit.ExpectedEvidence, view.ExpectedEvidence);
        Assert.Equal(edit.DueOn, view.DueOn);
        Assert.Equal(revisedOwner.MemberId, view.AccountableMemberId);
        Assert.Equal([evidence], view.EvidenceRequestIds);
        Assert.Equal("open", view.Status);
        Assert.Equal(2, action.Revision);
        var revised = Assert.IsType<RiskTreatmentActionRevised>(action.Latest!);
        Assert.Equal(edit.CommandId, revised.RequestId);
        Assert.Equal(view.CreatedBy, action.Added!.Operation.Actor);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.AssessorUserId).ToString(), revised.RevisedBy.Id);
    }

    [Fact]
    public async Task ShouldReplayTreatmentActionEditAndRejectChangedContentGivenRequestId()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [], 0)).ExpectSuccess();
        var edit = new ReviseRiskTreatmentAction(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, added.Value.ActionId, 1, "Updated action", "Updated state", "Updated evidence",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), worker.MemberId);

        // Act
        await fixture.Scenario(fixture.AssessorUserId).When(edit).ExpectSuccess();
        await fixture.Scenario(fixture.AssessorUserId).When(edit).ExpectSuccess();
        await fixture.Scenario(fixture.AssessorUserId).When(edit with { Title = "Changed" }).ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        var action = await fixture.ActionAsync(added.Value.ActionId);
        Assert.Equal(2, action.Revision);
        Assert.Equal("Updated action", Assert.Single((await fixture.GovernanceAsync()).TreatmentActions).Title);
        Assert.Single((await RiskTreatmentActionHistory.ReadAsync(
            provider.GetRequiredService<IDomainEventReader>(), action)).OfType<RiskTreatmentActionRevised>());
    }

    [Fact]
    public async Task ShouldRejectTreatmentActionEditGivenStaleRevisionOrSubmittedAction()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var first = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [], 0)).ExpectSuccess();
        var evidence = await fixture.SeedEvidenceAsync(true, worker);
        var second = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [evidence], 0)).ExpectSuccess();
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(second.Value.ActionId, 1, [evidence]));
        var edit = new ReviseRiskTreatmentAction(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, first.Value.ActionId, 0, "Stale edit", "State", "Evidence",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), worker.MemberId);

        // Act
        await fixture.Scenario(fixture.AssessorUserId).When(edit).ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AssessorUserId).When(edit with
        {
            CommandId = Uuid.CreateVersion4(),
            ActionId = second.Value.ActionId,
            ExpectedRevision = 2
        }).ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(1, (await fixture.ActionAsync(first.Value.ActionId)).Revision);
        Assert.Equal(2, (await fixture.ActionAsync(second.Value.ActionId)).Revision);
        Assert.All((await fixture.GovernanceAsync()).TreatmentActions, action => Assert.Equal("Enforce MFA", action.Title));
    }

    [Fact]
    public async Task ShouldReopenActionAndReportOverdueGivenRejectedCompletionPastDueDate()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(true, worker);
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [evidence], 0)).ExpectSuccess();
        var submission = await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 1, [evidence]));
        await fixture.AssignReviewAsync(submission.Value.SubmissionId);

        // Act
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.ApproverUserId,
            fixture.ReviewAction(added.Value.ActionId, 2, "reject"));
        await using var scope = provider.CreateAsyncScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>()
            .ReadRiskAsync(fixture.TenantId, fixture.ProgramId, fixture.RiskId);
        var late = snapshot.View(fixture.RiskId, [], DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60));

        // Assert
        var action = Assert.Single(late.TreatmentActions);
        Assert.Equal("open", action.Status);
        Assert.True(action.Overdue);
        Assert.Equal("overdue", late.TreatmentActionStatus);
        Assert.Equal("reject", Assert.Single(action.Completions).ReviewOutcome);
        Assert.Null((await fixture.ActionAsync(added.Value.ActionId)).PendingReview);
    }

    [Fact]
    public async Task ShouldRejectActionGivenPastDueDateNonTreatingRiskOrStaleRevision()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var action = fixture.AddAction(worker, [], 0);
        var accepting = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var acceptingProvider = accepting.Provider;
        var other = await accepting.SeedMemberAsync();

        // Act
        await fixture.Scenario(fixture.AssessorUserId).When(action with { DueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) }).ExpectFailure(RequestErrorKind.Validation);
        await accepting.Scenario(accepting.AssessorUserId).When(accepting.AddAction(other, [], 0)).ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AssessorUserId).When(action with { ActionId = Uuid.CreateVersion4(), ExpectedRevision = 5 }).ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.AssessorUserId).When(action with { ActionId = Uuid.CreateVersion4(), Title = " " }).ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.Equal("none", (await fixture.GovernanceAsync()).TreatmentActionStatus);
        Assert.Empty((await fixture.GovernanceAsync()).TreatmentActions);
    }

    static RiskTreatmentAssertionRules NewLine() => new(TenantId, ProgramId, RiskId, ControlId);

    static Uuid Propose(RiskTreatmentAssertionRules line, ulong revision)
    {
        var id = Uuid.CreateVersion4();
        Assert.True(line.Propose(Operation(line, id, revision, Proposer), Content, ProposerId, Content, Content).IsSuccess);
        return id;
    }

    static OperationProvenance Operation(RiskTreatmentAssertionRules owner, Uuid id, ulong expected, ActorReference actor) =>
        new(id, id, null, "RiskOwnerTest", actor, owner.Stream, "0", expected, new string('a', 64), Now, owner.Stream, owner.Stream);

    static OperationProvenance Operation(Aggregate owner, Uuid id, ulong expected, ActorReference actor) =>
        new(id, id, null, "RiskOwnerTest", actor, owner.Stream, "0", expected, new string('a', 64), Now, owner.Stream, owner.Stream);

    static ProposeRiskControlTreatment Proposal(RiskGovernanceHandlerTests.Fixture fixture) => new(Uuid.CreateVersion4(),
        fixture.TenantId, fixture.ProgramId, fixture.RiskId, 0, fixture.ControlId, fixture.ControlVersionId, "Treats risk.");

    static RiskAssessmentView Assessment(DateTimeOffset at) => new(Uuid.CreateVersion4(), "inherent", Uuid.CreateVersion4(),
        1, 3, 3, 9, "Assessed", Proposer, at);

    static async Task<Uuid> TreatmentWaiverAsync(RiskGovernanceHandlerTests.Fixture fixture, Uuid treatmentId, long revision)
    {
        var id = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId, id), waiver =>
        {
            Assert.Null(waiver.Record(new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.RiskControlTreatment,
                treatmentId, fixture.ControlVersionId, revision, SeparationOfDutiesActions.Review),
                RbacIds.Member(fixture.TenantId, fixture.AssessorUserId), Uuid.CreateVersion4(), "Requester", "No other reviewer.",
                now.AddMinutes(-2), now.AddDays(1)));
            Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", now.AddMinutes(-1)));
            return Result.Success;
        });
        return id;
    }
}
