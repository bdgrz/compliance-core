using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed class ControlOccurrenceTests
{
    [Fact]
    public async Task ShouldRecordExactVersionAndUnresolvedSupportGivenCompleteAttestation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        // Act
        var attested = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));

        // Assert
        Assert.Equal("submitted", attested.State);
        Assert.Equal(2, attested.Revision);
        var attestation = Assert.Single(attested.Attestations);
        Assert.Equal(fixture.ControlVersionId, attestation.ControlVersionId);
        Assert.Equal(plan.PlanVersionId, attestation.PlanVersionId);
        Assert.Equal(plan.ExpectedEvidence, attestation.ExpectedEvidence);
        Assert.Equal(missed.PeriodStart, attestation.CoveredFrom);
        Assert.Equal(missed.PeriodEnd, attestation.CoveredUntil);
        Assert.All(attestation.Evidence, evidence => Assert.Equal("unresolved", evidence.Resolution));
        Assert.Equal(new OperatingHolder("member", fixture.OwnerMemberId), attestation.PerformedBy);
        Assert.Equal(fixture.OwnerMemberId, attestation.RecorderMemberId);
    }

    [Fact]
    public async Task ShouldRejectCompletionGivenMissingSupportOrRationale()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        // Act
        // Assert
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId, fixture.Attest(missed, evidence: [new(0, "external", "https://x.example")]), RequestErrorKind.Validation);
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId, fixture.Attest(missed, "skipped", evidence: []), RequestErrorKind.Validation);
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId, fixture.Attest(missed, "partly"), RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRejectAttestationGivenMemberWithoutOperatingResponsibility()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        // Act
        // Assert
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OutsiderUserId, fixture.Attest(missed), RequestErrorKind.Forbidden);
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.LeadUserId, fixture.Attest(missed), RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRejectSecondAttestationGivenConcurrentSubmission()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));

        // Act
        // Assert
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.BackupUserId, fixture.Attest(missed), RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldAttributeRecorderAndPerformerGivenOffProductPersonPerformance()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(owner: new OperatingHolder("person", fixture.PersonId));
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        // Act
        var attested = await fixture.AsAsync(fixture.LeadUserId,
            fixture.Attest(missed, personId: fixture.PersonId));

        // Assert
        var attestation = Assert.Single(attested.Attestations);
        Assert.Equal(new OperatingHolder("person", fixture.PersonId), attestation.PerformedBy);
        Assert.Equal(fixture.LeadMemberId, attestation.RecorderMemberId);
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.LeadUserId, fixture.Review(attested, "approved"), RequestErrorKind.Forbidden);
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OutsiderUserId, fixture.Attest(missed, personId: fixture.PersonId), RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldSeparatePerformanceFromIndependentReviewGivenSubmission()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId, fixture.Review(submitted, "approved"), RequestErrorKind.Forbidden);
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OutsiderUserId, fixture.Review(submitted, "approved"), RequestErrorKind.Forbidden);

        // Act
        var reviewed = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "approved"));

        // Assert
        Assert.Equal("approved", reviewed.State);
        var review = Assert.Single(reviewed.Reviews);
        Assert.Equal(submitted.Attestations[0].AttestationId, review.AttestationId);
        Assert.Equal(fixture.ReviewerMemberId, review.ReviewerMemberId);
        var reviewerWork = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListMyControlWork(fixture.TenantId, fixture.ProgramId));
        Assert.DoesNotContain(reviewerWork.Items, item => item.SourceId == submitted.OccurrenceId);
    }

    [Fact]
    public async Task ShouldAllowWaivedSelfReviewGivenExactAttestationScope()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(owner: new OperatingHolder("person", fixture.PersonId));
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.LeadUserId,
            fixture.Attest(missed, personId: fixture.PersonId));
        var attestation = submitted.Attestations[0];
        var waiverId = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.ControlOccurrence, submitted.OccurrenceId,
            attestation.AttestationId, attestation.Version, SeparationOfDutiesActions.Review),
            fixture.LeadMemberId);

        // Act
        var reviewed = await fixture.AsAsync(fixture.LeadUserId,
            fixture.Review(submitted, "approved", waiverId: waiverId));

        // Assert
        Assert.Equal(waiverId, reviewed.Reviews[0].SeparationOfDutiesWaiverId);
    }

    [Fact]
    public async Task ShouldKeepCorrectionHistoryGivenReturnedAttestation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        var returned = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "returned"));

        // Act
        var corrected = await fixture.AsAsync(fixture.OwnerUserId, new CorrectControlAttestation(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, returned.OccurrenceId,
            returned.Revision, "complete", DateTimeOffset.UtcNow.AddMinutes(-1), null, null,
            "Added the missing ticket.", null, OperationsFixture.FullSupport,
            "Reviewer found the removal ticket missing."));

        // Assert
        Assert.Equal("submitted", corrected.State);
        Assert.Equal(2, corrected.Attestations.Count);
        Assert.Equal(corrected.Attestations[0].AttestationId,
            corrected.Attestations[1].SupersedesAttestationId);
        Assert.Equal("Reviewer found the removal ticket missing.",
            corrected.Attestations[1].CorrectionReason);
        Assert.Equal("returned", Assert.Single(corrected.Reviews).Outcome);
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId, fixture.Review(corrected, "approved") with
        {
            AttestationId = corrected.Attestations[0].AttestationId,
        }, RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRecordRequestedActionsGivenActionRequestedReview()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId, fixture.Review(submitted, "action_requested"), RequestErrorKind.Validation);

        // Act
        var reviewed = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "action_requested", ["Automate leaver removal."]));

        // Assert
        Assert.Equal("action_requested", reviewed.State);
        Assert.Equal(["Automate leaver removal."], reviewed.Reviews[0].RequestedActions);
    }

    [Fact]
    public async Task ShouldOpenEventDrivenOccurrenceGivenTriggerAndRejectRecurringControl()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(cadence: new ControlCadence("event_driven",
            Trigger: "a production change ships", DueWithinDays: 2));

        // Act
        var opened = await fixture.AsAsync(fixture.OwnerUserId, fixture.Open("Release 4.2", fixture.Today));

        // Assert
        Assert.Equal("event_driven", opened.Kind);
        Assert.Equal("open", opened.State);
        Assert.Equal(fixture.Today.AddDays(2), opened.DueOn);
        Assert.Equal(opened.OccurrenceId, Assert.Single(await fixture.OccurrencesAsync()).OccurrenceId);
        await fixture.Scenario(fixture.OutsiderUserId).When(fixture.Open("Release 4.3", fixture.Today))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRejectOpeningGivenRecurringCadence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(fixture.Open("Extra run", fixture.Today))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
    }
}
