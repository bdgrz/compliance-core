using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Queries;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingTests
{
    [Fact]
    public async Task ShouldReturnOriginalViewGivenExactRevisionRetryAfterLaterChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.LeadMemberId);
        var first = new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, finding.FindingId, 1,
            "critical", fixture.LeadMemberId, fixture.Today.AddDays(6), "All", null, "First triage");
        var original = await fixture.AsAsync(fixture.LeadUserId, first);
        await fixture.AsAsync(fixture.LeadUserId, first with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 2,
            Severity = "low",
            Reason = "Later triage",
        });
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Member(fixture.TenantId, fixture.OwnerUserId),
            member => member.Suspend(fixture.LeadMemberId, "Lead", DateTimeOffset.UtcNow, "Owner left"));
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new FindingAggregate(fixture.TenantId, fixture.ProgramId, finding.FindingId));

        // Act
        var retry = await fixture.AsAsync(fixture.LeadUserId, first);
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new FindingAggregate(fixture.TenantId, fixture.ProgramId, finding.FindingId));

        // Assert
        Assert.Equal(WireJson.Of(original), WireJson.Of(retry));
        Assert.Equal(2, retry.Revision);
        Assert.Equal(before.Version, after.Version);
    }

    [Fact]
    public async Task ShouldConflictGivenRevisionRetryWithChangedIntent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.LeadMemberId);
        var revise = new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, finding.FindingId, 1,
            "critical", fixture.LeadMemberId, fixture.Today.AddDays(6), "All", null, "First triage");
        await fixture.AsAsync(fixture.LeadUserId, revise);

        // Act
        var changed = fixture.Scenario(fixture.LeadUserId).When(revise with { Severity = "low" });

        // Assert
        await changed.ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldCloseAndReopenWithHistoryGivenVerifiedRemediation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await RaiseWithCompletedActionAsync(fixture);

        // Act
        var closed = await fixture.CloseAsync(fixture.ApproverUserId, finding);
        var reopened = await fixture.AsAsync(fixture.LeadUserId, new ReopenFinding(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, finding.FindingId, closed.Revision, "A leaver regained access."));

        // Assert
        Assert.Equal("closed", closed.Status);
        Assert.Equal("closed", closed.ReadinessStatus);
        Assert.Equal(fixture.ApproverMemberId, closed.Closure!.CloserMemberId);
        Assert.All(closed.Closure.ResolutionEvidence, item => Assert.Equal("unresolved", item.Resolution));
        Assert.Equal("remediated", reopened.Status);
        Assert.Null(reopened.Closure);
        Assert.Equal(["raised", "corrective_action_added", "corrective_action_completed", "closed", "reopened"],
            reopened.History.Select(entry => entry.Change));
    }

    [Fact]
    public async Task ShouldRejectClosureGivenAcceptanceInsteadOfRemediation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var waiverId = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Finding, finding.FindingId, finding.FindingId, 1,
            SeparationOfDutiesActions.Approve), fixture.LeadMemberId);
        var accepted = await fixture.AsAsync(fixture.LeadUserId, new LinkFindingAcceptance(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, finding.FindingId, 1, "waiver", waiverId));

        // Act
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId,
            fixture.Close(accepted), RequestErrorKind.Conflict);

        // Assert
        Assert.Equal("accepted", accepted.ReadinessStatus);
        Assert.Equal("active", Assert.Single(accepted.Acceptances).State);
    }

    [Fact]
    public async Task ShouldReturnToActionableStateGivenExpiredAcceptance()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var waiverId = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Finding, finding.FindingId, finding.FindingId, 1,
            SeparationOfDutiesActions.Approve), fixture.LeadMemberId);
        await fixture.AsAsync(fixture.LeadUserId, new LinkFindingAcceptance(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, 1, "waiver", waiverId));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<FindingReader>();
        var now = DateTimeOffset.UtcNow;

        // Act
        var during = await reader.ReadViewAsync(fixture.TenantId, fixture.ProgramId, finding.FindingId, now.AddDays(1));
        var after = await reader.ReadViewAsync(fixture.TenantId, fixture.ProgramId, finding.FindingId, now.AddDays(31));

        // Assert
        Assert.Equal("accepted", during!.ReadinessStatus);
        Assert.Equal("overdue", after!.ReadinessStatus);
        Assert.Equal("expired", after.Acceptances[0].State);
    }

    [Fact]
    public async Task ShouldRejectClosureGivenOwnerVerifiesOwnRemediation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await RaiseWithCompletedActionAsync(fixture, fixture.LeadMemberId);

        // Act
        var owner = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.LeadUserId,
            fixture.Close(finding), RequestErrorKind.Forbidden);
        var performer = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider,
            fixture.OwnerUserId, fixture.Close(finding), RequestErrorKind.Forbidden);

        // Assert
        Assert.False(owner.IsSuccess);
        Assert.False(performer.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectClosureGivenMissingResolutionEvidence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await RaiseWithCompletedActionAsync(fixture);

        // Act
        var result = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider,
            fixture.ApproverUserId, fixture.Close(finding) with { ResolutionEvidence = [] }, RequestErrorKind.Validation);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldPreserveSourceTextAndAttributeChangeGivenTriageRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);

        // Act
        var revised = await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, finding.FindingId, 1, "critical", fixture.BackupMemberId,
            fixture.Today.AddDays(3), "VPN and SSO", "Manual offboarding", "Scope widened after triage."));

        // Assert
        Assert.Equal("Terminated users kept VPN access.", revised.Source.SourceText);
        Assert.Equal("critical", revised.Severity);
        Assert.Equal(fixture.BackupMemberId, revised.OwnerMemberId);
        var change = revised.History[^1];
        Assert.Equal("revised", change.Change);
        Assert.Equal(fixture.LeadMemberId.ToString(), change.Actor.Id);
        Assert.Contains("Scope widened after triage.", change.Summary, StringComparison.Ordinal);
        await fixture.Scenario(fixture.OwnerUserId).When(new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId,
                fixture.ProgramId, finding.FindingId, 2, "low", fixture.OwnerMemberId, fixture.Today, "VPN", null,
                "Downgrade."))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRejectGovernedSourceGivenUnknownRecord()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        await fixture.Scenario(fixture.LeadUserId)
            // Act
            .When(fixture.Raise(fixture.OwnerMemberId) with
            {
                Source = new FindingSource("readiness_gap", Uuid.CreateVersion4(),
                    Uuid.CreateVersion4().ToString(), "Unmapped criterion."),
            })
            // Assert
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.LeadUserId)
            .When(fixture.Raise(fixture.OwnerMemberId) with
            {
                Source = new FindingSource("control_occurrence", Uuid.CreateVersion4(), null, "Failed."),
            })
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRouteCorrectiveActionToOwnerQueueGivenAssignment()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var raised = await fixture.RaiseFindingAsync(fixture.LeadMemberId);

        // Act
        var finding = await fixture.AddActionAsync(raised, fixture.OwnerMemberId, fixture.Today.AddDays(-1),
            "Remove stale VPN accounts.");

        // Assert
        var work = await fixture.AsAsync(fixture.OwnerUserId, new ListMyControlWork(fixture.TenantId, fixture.ProgramId));
        var item = Assert.Single(work.Items);
        Assert.Equal("corrective_action", item.Kind);
        Assert.Equal(finding.FindingId, item.FindingId);
        Assert.Equal("high", item.Materiality);
        Assert.True(item.Overdue);
        var action = Assert.Single(finding.CorrectiveActions);
        Assert.Equal(1, action.Revision);
        await fixture.Scenario(fixture.OutsiderUserId).When(new CompleteCorrectiveAction(Uuid.CreateVersion4(),
                fixture.TenantId, fixture.ProgramId, finding.FindingId, 1, action.ActionId, "Done.",
                [new EvidenceReference(null, "record", "VPN-EXPORT")]))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldListByReadinessStatusGivenMixedFindings()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.RaiseFindingAsync(fixture.OwnerMemberId, fixture.Today.AddDays(-3));
        await RaiseWithCompletedActionAsync(fixture);

        // Act
        var overdue = await fixture.AsAsync(fixture.OutsiderUserId, new ListFindings(fixture.TenantId,
            fixture.ProgramId, "overdue"));
        var remediated = await fixture.AsAsync(fixture.OutsiderUserId, new ListFindings(fixture.TenantId,
            fixture.ProgramId, "remediated"));

        // Assert
        Assert.Single(overdue.Items);
        Assert.Single(remediated.Items);
        await fixture.Scenario(fixture.OutsiderUserId).When(new ListFindings(fixture.TenantId, fixture.ProgramId,
                "maybe"))
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionGivenConcurrentFindingChanges()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(5), "Fix.");

        await fixture.Scenario(fixture.ApproverUserId)
            // Act
            .When(new AddCorrectiveAction(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, finding.FindingId,
                1, "Fix again.", fixture.OwnerMemberId, fixture.Today.AddDays(5)))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    internal static async Task<FindingView> RaiseWithCompletedActionAsync(OperationsFixture fixture,
        Uuid? findingOwner = null)
    {
        var raised = await fixture.RaiseFindingAsync(findingOwner ?? fixture.OwnerMemberId);
        var finding = await fixture.AddActionAsync(raised, fixture.OwnerMemberId, fixture.Today.AddDays(7),
            "Remove stale VPN accounts.");
        return await fixture.CompleteActionAsync(fixture.OwnerUserId, finding, finding.CorrectiveActions[0].ActionId,
            "Removed both accounts.");
    }
}
