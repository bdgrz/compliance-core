using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlLifecycleHandlerTests
{
    static readonly DateOnly EffectiveFrom = new(2026, 10, 1);
    static readonly DateOnly SuccessorFrom = new(2027, 1, 1);
    static readonly Uuid ApplicationId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldApproveSuccessorGivenMatchingImpactDigestAndRejectStaleDigest()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var registration = await fixture.ProposeSuccessorAsync();
        await fixture.AssignOwnerAsync(registration.DraftVersionId, registration.Revision);
        var reviewId = await fixture.ReviewAsync(registration.Revision);
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                registration.Revision));

        // Act
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, fixture.Approve(registration.Revision, reviewId, SuccessorFrom, "STALE"), RequestErrorKind.Conflict);
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, fixture.Approve(registration.Revision, reviewId, SuccessorFrom, preview.Digest));

        // Assert
        Assert.Equal("successor", preview.Kind);
        Assert.True(preview.Complete);
        Assert.Empty(preview.PendingContexts);
        Assert.Contains(preview.Changes, change => change.Field == "title");
        Assert.Contains(preview.Changes, change =>
            change.Field == "applicability" && change.ChangeType == "added");
        var applicability = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "applicability");
        Assert.Equal(ApplicationId, Assert.Single(applicability.Records).RecordId);
        var responsibilities = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "responsibilities");
        Assert.Contains(responsibilities.Records, record =>
            record.VersionId == fixture.InitialVersionId);
        var mappings = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "mappings");
        Assert.Equal("complete", mappings.Status);
        Assert.Empty(mappings.Records);
        var versions = await fixture.QueryAsync<ListControlVersions, Page<ControlVersionView>>(
            new ListControlVersions(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        Assert.Equal(["superseded", "approved"], versions.Items.Select(static v => v.Status));
        var before = await fixture.QueryAsync<GetEffectiveControlVersion, ControlVersionView>(
            new GetEffectiveControlVersion(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, SuccessorFrom.AddDays(-1)));
        Assert.Equal(fixture.InitialVersionId, before.VersionId);
        var exact = await fixture.QueryAsync<GetControlVersion, ControlVersionView>(
            new GetControlVersion(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                registration.DraftVersionId));
        Assert.Equal(fixture.InitialVersionId, exact.PredecessorVersionId);
    }

    [Fact]
    public async Task ShouldRetireGivenMatchingDigestAndKeepHistoricalReads()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var retireOn = new DateOnly(2027, 3, 1);
        var proposal = await fixture.Scenario(fixture.AuthorUserId)
            .When(new ProposeControlRetirement(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, fixture.InitialVersionId, retireOn, "Replaced."))
            .ExpectSuccess();
        var reviewId = await fixture.ReviewAsync(proposal.Value.Revision);
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Value.Revision));

        // Act
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, new RetireControl(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Value.Revision, reviewId, preview.Digest, "Retire at quarter end."));

        // Assert
        Assert.Equal("retirement", preview.Kind);
        Assert.Equal(proposal.Value.RetirementId, preview.TargetId);
        var current = await fixture.QueryAsync<GetCurrentControlVersion, ControlVersionView>(
            new GetCurrentControlVersion(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        Assert.Equal("retired", current.Status);
        Assert.Equal(retireOn, current.EffectiveUntil);
        var historical = await fixture.QueryAsync<GetEffectiveControlVersion, ControlVersionView>(
            new GetEffectiveControlVersion(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, retireOn.AddDays(-1)));
        Assert.Equal(fixture.InitialVersionId, historical.VersionId);
        var after = await fixture.Scenario(fixture.ApproverUserId)
            .When(new GetEffectiveControlVersion(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, retireOn))
            .ExpectFailure();
        Assert.Equal(RequestErrorKind.NotFound, after.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRejectSuccessorProposalGivenDisabledLifecycleGate()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: false);

        await fixture.Scenario(fixture.AuthorUserId)
            // Act
            .When(new ProposeControlSuccessor(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, fixture.InitialVersionId, Fixture.Content()))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectFailure(RequestErrorKind.Conflict);
        var source = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlRules(fixture.TenantId, fixture.ControlId));
        Assert.False(source.HasOpenDraft);
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenStaleRevision()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var registration = await fixture.ProposeSuccessorAsync();

        // Act
        var result = await fixture.Scenario(fixture.ApproverUserId)
            .When(new PreviewControlImpact(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, registration.Revision - 1))
            .ExpectFailure();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldReportMappingsAndRiskTreatmentsGivenRecordsNamingControl()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var mappingId = await fixture.SeedAcceptedMappingAsync();
        var treatmentId = await fixture.SeedAcceptedRiskTreatmentAsync();
        var registration = await fixture.ProposeSuccessorAsync();

        // Act
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                registration.Revision));

        // Assert
        var mappings = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "mappings");
        Assert.Equal("complete", mappings.Status);
        var mapping = Assert.Single(mappings.Records);
        Assert.Equal(mappingId, mapping.RecordId);
        Assert.Equal(fixture.InitialVersionId, mapping.VersionId);
        var treatments = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "risk_treatments");
        Assert.Equal(treatmentId, Assert.Single(treatments.Records).RecordId);
        var readiness = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "readiness");
        Assert.Equal("complete", readiness.Status);
        Assert.Equal(["engagements"], preview.Contributions
            .Where(static contribution => contribution.Status == "unlinked")
            .Select(static contribution => contribution.Context));
        Assert.True(preview.Complete);
    }

    [Fact]
    public async Task ShouldReportRetainedEvidenceAndChangeDigestGivenLinkedRequestMutation()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var requestId = await fixture.SeedEvidenceRequestAsync();
        var artifactId = Uuid.CreateVersion4();
        var fulfilledId = await fixture.SeedEvidenceRequestAsync(artifactId: artifactId);
        var cancelledId = await fixture.SeedEvidenceRequestAsync(cancel: true);
        await fixture.SeedEvidenceRequestAsync(controlId: Uuid.CreateVersion4());
        await fixture.SeedEvidenceRequestAsync(tenantId: Uuid.CreateVersion4());
        await fixture.SeedEvidenceRequestAsync(programId: Uuid.CreateVersion4());
        var proposal = await fixture.ProposeSuccessorAsync();
        var request = new PreviewControlImpact(fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, proposal.Revision);

        // Act
        var before = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(request);
        var unchanged = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(request);
        await fixture.CancelEvidenceRequestAsync(requestId);
        var after = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(request);

        // Assert
        var evidence = Assert.Single(before.Contributions,
            contribution => contribution.Context == "evidence");
        Assert.Equal("complete", evidence.Status);
        Assert.Equal("authoritative_source", evidence.Freshness);
        Assert.Equal(4, evidence.Records.Count);
        Assert.Contains(evidence.Records, record => record.RecordId == requestId);
        Assert.Contains(evidence.Records, record => record.RecordId == fulfilledId);
        Assert.Contains(evidence.Records, record => record.RecordId == cancelledId);
        Assert.Contains(evidence.Records, record =>
            record.RecordType == "evidence_artifact" && record.RecordId == artifactId);
        Assert.All(evidence.Records, record => Assert.Equal(fixture.TenantId, record.TenantId));
        Assert.Equal(before.Digest, unchanged.Digest);
        Assert.NotEqual(before.Digest, after.Digest);
        Assert.True(after.Complete);
    }

    [Fact]
    public async Task ShouldReportRetainedWorkAndCadenceGivenRecordedControlSources()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var planId = await fixture.SeedOperatingPlanAsync();
        var occurrenceId = await fixture.SeedOccurrenceAsync(complete: true);
        var currentPlanId = await fixture.SeedOperatingPlanAsync(effectiveFrom: EffectiveFrom.AddMonths(1));
        var pendingPlanId = await fixture.SeedOperatingPlanAsync(approve: false,
            effectiveFrom: EffectiveFrom.AddMonths(2));
        var actionId = await fixture.SeedCorrectiveActionAsync(complete: true);
        var evaluationId = await fixture.SeedEvaluationAsync();
        await fixture.SeedCorrectiveActionAsync(controlId: Uuid.CreateVersion4());
        await fixture.SeedCorrectiveActionAsync(tenantId: Uuid.CreateVersion4());
        await fixture.SeedCorrectiveActionAsync(programId: Uuid.CreateVersion4());
        var proposal = await fixture.ProposeSuccessorAsync();

        // Act
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Revision));

        // Assert
        var work = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "work");
        Assert.Equal("complete", work.Status);
        Assert.Equal("authoritative_source", work.Freshness);
        Assert.Equal(7, work.Records.Count);
        Assert.Contains(work.Records, record => record.RecordId == planId &&
            record.RecordType == "control_operating_plan" &&
            record.VersionId == fixture.InitialVersionId);
        Assert.Contains(work.Records, record => record.RecordId == pendingPlanId &&
            record.Reason.Contains("pending_approval", StringComparison.Ordinal));
        Assert.Contains(work.Records, record => record.RecordId == planId &&
            record.Reason.Contains("superseded", StringComparison.Ordinal));
        Assert.Contains(work.Records, record => record.RecordId == currentPlanId);
        Assert.Contains(work.Records, record => record.RecordId == occurrenceId &&
            record.Reason.Contains("approved", StringComparison.Ordinal));
        Assert.Contains(work.Records, record => record.RecordId == actionId &&
            record.Reason.Contains("completed", StringComparison.Ordinal));
        Assert.Contains(work.Records, record => record.RecordId == evaluationId &&
            record.VersionId == fixture.InitialVersionId);
        Assert.All(work.Records, record => Assert.Equal(fixture.TenantId, record.TenantId));
        Assert.True(preview.Complete);
    }

    [Fact]
    public async Task ShouldReportRetainedSupportGivenAttestationActionAndEvaluationArtifactReferences()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var artifactId = Uuid.CreateVersion4();
        await fixture.SeedOperatingPlanAsync();
        await fixture.SeedOccurrenceAsync(artifactId: artifactId);
        await fixture.SeedOccurrenceAsync(evidenceReference: "unresolved-artifact-reference");
        var actionId = await fixture.SeedCorrectiveActionAsync(complete: true, artifactId: artifactId);
        var evaluationId = await fixture.SeedEvaluationAsync(artifactId);
        var proposal = await fixture.ProposeSuccessorAsync();

        // Act
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Revision));

        // Assert
        var evidence = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "evidence");
        Assert.Equal("complete", evidence.Status);
        Assert.Equal(5, evidence.Records.Count);
        Assert.Equal(2, evidence.Records.Count(record => record.RecordType == "control_attestation"));
        Assert.Contains(evidence.Records, record => record.RecordId == actionId);
        Assert.Contains(evidence.Records, record => record.RecordId == evaluationId);
        var artifact = Assert.Single(evidence.Records,
            record => record.RecordType == "evidence_artifact");
        Assert.Equal(artifactId, artifact.RecordId);
        Assert.Contains("sha256:original", artifact.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportCorrectiveWorkGivenIdentifierAndSourceOnlyControlRelationships()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        await fixture.SeedOperatingPlanAsync();
        var occurrenceId = await fixture.SeedOccurrenceAsync();
        var identifierActionId = await fixture.SeedCorrectiveActionAsync(linkReference: "lc-http");
        var sourceActionId = await fixture.SeedCorrectiveActionAsync(sourceOccurrenceId: occurrenceId);
        var proposal = await fixture.ProposeSuccessorAsync();

        // Act
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Revision));

        // Assert
        var work = Assert.Single(preview.Contributions, contribution => contribution.Context == "work");
        Assert.Contains(work.Records, record => record.RecordId == identifierActionId);
        Assert.Contains(work.Records, record => record.RecordId == sourceActionId);
    }

    [Fact]
    public async Task ShouldRetainClosureEvidenceGivenLinkedFindingReopened()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var artifactId = Uuid.CreateVersion4();
        var actionId = await fixture.SeedCorrectiveActionAsync(complete: true);
        var closureId = await fixture.CloseAndReopenFindingAsync(actionId, artifactId);
        var proposal = await fixture.ProposeSuccessorAsync();

        // Act
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Revision));

        // Assert
        var evidence = Assert.Single(preview.Contributions, contribution => contribution.Context == "evidence");
        Assert.Contains(evidence.Records, record => record.RecordType == "finding_closure" &&
            record.RecordId == closureId);
        Assert.Contains(evidence.Records, record => record.RecordType == "evidence_artifact" &&
            record.RecordId == artifactId);
    }

    [Fact]
    public async Task ShouldRejectSuccessorGivenEvidenceSourceChangedAfterPreview()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var evidenceId = await fixture.SeedEvidenceRequestAsync();
        var proposal = await fixture.ProposeSuccessorAsync();
        await fixture.AssignOwnerAsync(proposal.DraftVersionId, proposal.Revision);
        var reviewId = await fixture.ReviewAsync(proposal.Revision);
        var query = new PreviewControlImpact(fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, proposal.Revision);
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(query);

        // Act
        await fixture.CancelEvidenceRequestAsync(evidenceId);
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, fixture.Approve(proposal.Revision, reviewId, SuccessorFrom, preview.Digest), RequestErrorKind.Conflict);
        var refreshed = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(query);
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, fixture.Approve(proposal.Revision, reviewId, SuccessorFrom, refreshed.Digest));

        // Assert
        Assert.NotEqual(preview.Digest, refreshed.Digest);
        var current = await fixture.QueryAsync<GetCurrentControlVersion, ControlVersionView>(
            new GetCurrentControlVersion(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        Assert.Equal(proposal.DraftVersionId, current.VersionId);
    }

    [Fact]
    public async Task ShouldRejectRetirementGivenWorkSourceChangedAfterPreview()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        await fixture.SeedOperatingPlanAsync();
        var proposal = await fixture.Scenario(fixture.AuthorUserId)
            .When(new ProposeControlRetirement(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, fixture.InitialVersionId, SuccessorFrom, "Replaced."))
            .ExpectSuccess();
        var reviewId = await fixture.ReviewAsync(proposal.Value.Revision);
        var query = new PreviewControlImpact(fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, proposal.Value.Revision);
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(query);

        // Act
        await fixture.SeedOperatingPlanAsync(approve: false, effectiveFrom: EffectiveFrom.AddMonths(1));
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, new RetireControl(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Value.Revision, reviewId, preview.Digest, "Retire."), RequestErrorKind.Conflict);
        var refreshed = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(query);
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, new RetireControl(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Value.Revision, reviewId, refreshed.Digest, "Retire."));

        // Assert
        Assert.NotEqual(preview.Digest, refreshed.Digest);
        var current = await fixture.QueryAsync<GetCurrentControlVersion, ControlVersionView>(
            new GetCurrentControlVersion(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        Assert.Equal("retired", current.Status);
    }

    [Theory]
    [InlineData("successor", "evidence")]
    [InlineData("retirement", "evidence")]
    [InlineData("successor", "work")]
    [InlineData("retirement", "work")]
    public async Task ShouldBlockDecisionGivenCombinedImpactContextExceedsBound(string kind, string context)
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        if (context == "work")
            await fixture.SeedOperatingPlanAsync();
        for (var index = context == "work" ? 1 : 0; index < (context == "work" ? 200 : 100); index++)
            if (context == "work")
                await fixture.SeedOccurrenceAsync();
            else
                await fixture.SeedEvidenceRequestAsync(artifactId: Uuid.CreateVersion4());
        long revision;
        if (kind == "successor")
        {
            var proposal = await fixture.ProposeSuccessorAsync();
            revision = proposal.Revision;
            await fixture.AssignOwnerAsync(proposal.DraftVersionId, revision);
        }
        else
        {
            var proposal = await fixture.Scenario(fixture.AuthorUserId)
                .When(new ProposeControlRetirement(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                    fixture.ControlId, fixture.InitialVersionId, SuccessorFrom, "Replaced."))
                .ExpectSuccess();
            revision = proposal.Value.Revision;
        }
        var reviewId = await fixture.ReviewAsync(revision);
        var query = new PreviewControlImpact(fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, revision);
        var atBound = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(query);

        // Act
        if (context == "work")
            await fixture.SeedOccurrenceAsync();
        else
            await fixture.SeedEvidenceRequestAsync();
        var overflow = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(query);
        if (kind == "successor")
            await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, fixture.Approve(revision, reviewId, SuccessorFrom, overflow.Digest), RequestErrorKind.Conflict);
        else
            await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, new RetireControl(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                    revision, reviewId, overflow.Digest, "Retire."), RequestErrorKind.Conflict);

        // Assert
        Assert.True(atBound.Complete);
        Assert.Equal(200, Assert.Single(atBound.Contributions,
            contribution => contribution.Context == context).Records.Count);
        Assert.False(overflow.Complete);
        Assert.Contains(context, overflow.PendingContexts);
        var contribution = Assert.Single(overflow.Contributions, item => item.Context == context);
        Assert.Equal("bounded_preview_exceeded", contribution.Freshness);
        Assert.Equal(200, contribution.Records.Count);
        Assert.False(contribution.Complete);
    }

    [Fact]
    public async Task ShouldKeepForeignControlUndisclosedGivenImpactQuery()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        await fixture.SeedEvidenceRequestAsync();
        var proposal = await fixture.ProposeSuccessorAsync();

        // Act
        var foreignTenant = await fixture.Scenario(fixture.ApproverUserId)
            .When(new PreviewControlImpact(Uuid.CreateVersion4(), fixture.ProgramId,
                fixture.ControlId, proposal.Revision))
            .ExpectFailure(RequestErrorKind.NotFound);
        var foreignProgram = await fixture.Scenario(fixture.ApproverUserId)
            .When(new PreviewControlImpact(fixture.TenantId, Uuid.CreateVersion4(),
                fixture.ControlId, proposal.Revision))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Equal("The control was not found.", foreignTenant.Error!.Message);
        Assert.Equal(foreignTenant.Error.Message, foreignProgram.Error!.Message);
    }

    [Fact]
    public async Task ShouldRestoreApprovedContentGivenWithdrawnSuccessorOverHttp()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var registration = await fixture.ProposeSuccessorAsync();

        // Act
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(new WithdrawControlProposal(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, registration.Revision, "Not needed this quarter."))
            .ExpectSuccess();

        // Assert
        var source = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlRules(fixture.TenantId, fixture.ControlId));
        Assert.False(source.HasOpenDraft);
        Assert.Equal(registration.Revision + 1, source.Revision);
        Assert.Equal("Access review", source.CurrentContent!.Title);
        var preview = await fixture.Scenario(fixture.ApproverUserId)
            .When(new PreviewControlImpact(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, source.Revision))
            .ExpectFailure();
        Assert.Equal(RequestErrorKind.Conflict, preview.Error!.Kind);
        var decisions = await fixture.QueryAsync<ListControlDecisions,
            Page<ControlDecisionView>>(new ListControlDecisions(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId));
        Assert.Equal("withdrawal", decisions.Items[^1].Kind);
    }

    [Fact]
    public async Task ShouldActivateSuccessorGivenDesignatedPersonOwnerOverHttp()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var registration = await fixture.ProposeSuccessorAsync();
        var personId = await fixture.SeedPersonAsync();
        await fixture.Scenario(fixture.AuthorUserId)
            .When(new DesignateControlOwnerPerson(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, registration.Revision, Uuid.CreateVersion4(),
                "Unknown person."))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.AuthorUserId)
            .When(new DesignateControlOwnerPerson(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, registration.Revision, personId,
                "The facilities manager owns the control but does not sign in."))
            .ExpectSuccess();
        var reviewId = await fixture.ReviewAsync(registration.Revision);
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                registration.Revision));

        // Act
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, fixture.Approve(registration.Revision, reviewId, SuccessorFrom, preview.Digest));

        // Assert
        var current = await fixture.QueryAsync<GetCurrentControlVersion, ControlVersionView>(
            new GetCurrentControlVersion(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        Assert.Equal("verified_person", current.OwnerResolution);
        Assert.Equal(personId, current.OwnerPersonId);
        Assert.Equal(Uuid.Empty, current.OwnerMemberId);
    }

    [Fact]
    public async Task ShouldRejectWithdrawalGivenDisabledLifecycleGate()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: false);

        await fixture.Scenario(fixture.AuthorUserId)
            // Act
            .When(new WithdrawControlProposal(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, 1, "Nothing."))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    sealed class Fixture
    {
        public required ServiceProvider Provider { get; init; }
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid AuthorUserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
        public Uuid OwnerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "LC-HTTP");
        public Uuid InitialVersionId => ControlVersionIds.Initial(ControlId);
        Uuid OwnerMemberId => RbacIds.Member(TenantId, OwnerUserId);

        public static async Task<Fixture> CreateApprovedAsync(bool lifecycleEnabled)
        {
            var provider = ProgramManagementServices.Build(
                new RecordingPermissionAuthorizer(allowed: true),
                portia => portia.AddRequestHandler<ReviewControlHandler>()
                    .AddRequestHandler<ApproveControlHandler>()
                    .AddRequestHandler<ProposeControlSuccessorHandler>()
                    .AddRequestHandler<ProposeControlRetirementHandler>()
                    .AddRequestHandler<PreviewControlImpactHandler>()
                    .AddRequestHandler<RetireControlHandler>()
                    .AddRequestHandler<GetControlVersionHandler>()
                    .AddRequestHandler<GetCurrentControlVersionHandler>()
                    .AddRequestHandler<GetEffectiveControlVersionHandler>()
                    .AddRequestHandler<ListControlVersionsHandler>()
                    .AddRequestHandler<ListControlDecisionsHandler>()
                    .AddRequestHandler<WithdrawControlProposalHandler>()
                    .AddRequestHandler<DesignateControlOwnerPersonHandler>(),
                services => services
                    .AddSingleton(new ControlActivationReleaseGate(true))
                    .AddSingleton(new ControlLifecycleReleaseGate(lifecycleEnabled))
                    .AddScoped<ControlActivationSource>()
                    .AddScoped<ControlOperationsReader>()
                    .AddScoped<FindingReader>()
                    .AddScoped<ControlImpactService>()
                    .AddScoped<IControlImpactContributor, ApplicabilityControlImpactContributor>()
                    .AddScoped<IControlImpactContributor, ResponsibilityControlImpactContributor>()
                    .AddScoped<IControlImpactContributor, MappingControlImpactContributor>()
                    .AddScoped<IControlImpactContributor, RiskTreatmentControlImpactContributor>()
                    .AddScoped<IControlImpactContributor, ReadinessControlImpactContributor>()
                    .AddScoped<IControlImpactContributor, EvidenceControlImpactContributor>()
                    .AddScoped<IControlImpactContributor, WorkControlImpactContributor>()
                    .AddSingleton<IControlApplicabilityReferenceValidator, AcceptingValidator>());
            var fixture = new Fixture { Provider = provider };
            var now = DateTimeOffset.UtcNow;
            await ProgramManagementServices.SeedAsync(provider,
                new ControlRules(fixture.TenantId, fixture.ControlId), control => control.Create(
                    fixture.ProgramId, Uuid.CreateVersion4(), "LC-HTTP", Content(),
                    RbacIds.Member(fixture.TenantId, fixture.AuthorUserId), "Author", now));
            await ProgramManagementServices.SeedAsync(provider,
                new Member(fixture.TenantId, fixture.OwnerUserId), member => member.Register());
            await fixture.AssignOwnerAsync(fixture.InitialVersionId, 1);
            var reviewId = await fixture.ReviewAsync(1);
            await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, new ApproveControl(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                    1, reviewId, EffectiveFrom, "Ready to operate."));
            return fixture;
        }

        public static ControlDraftContent Content() => new("Access review", "Review access",
            "Management reviews access", "The security lead reviews access quarterly.",
            ["Dated review record"]);

        public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId));

        public ApproveControl Approve(long revision, Uuid reviewId, DateOnly effectiveFrom,
            string digest) => new(Uuid.CreateVersion4(), TenantId, ProgramId, ControlId, revision, reviewId,
            effectiveFrom, "Successor ready.", ImpactDigest: digest);

        public async Task<ControlSuccessorRegistration> ProposeSuccessorAsync()
        {
            var result = await Scenario(AuthorUserId)
                .When(new ProposeControlSuccessor(Uuid.CreateVersion4(), TenantId, ProgramId, ControlId,
                    InitialVersionId, Content() with
                    {
                        Title = "Access review v2",
                        Applicability =
                        [
                            new ControlApplicabilityReference(Uuid.CreateVersion4(),
                                "application", "Billing", ApplicationId,
                                "Billing access is reviewed.", false),
                        ],
                    }))
                .ExpectSuccess();
            return result.Value;
        }

        public Task AssignOwnerAsync(Uuid versionId, long revision)
        {
            var now = DateTimeOffset.UtcNow;
            return ProgramManagementServices.SeedAsync(Provider,
                new ControlRules(TenantId, ControlId), control =>
                    CommandResult(control.AssignResponsibility(new ResponsibilityScope("control",
                            ControlId, versionId, revision), Uuid.CreateVersion4(), OwnerMemberId,
                        ResponsibilityType.ControlOwner, RbacIds.Member(TenantId, AuthorUserId),
                        "Author", now, now.AddMinutes(-1), null, [])));
        }

        public async Task<Uuid> SeedAcceptedMappingAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var edition = Uuid.CreateVersion4();
            var mappingId = await MappingSeeds.ProposeMappingAsync(Provider, TenantId, ProgramId, ControlId,
                InitialVersionId, edition, "CC6.1", 0, "Reviews restrict access.", "Production.",
                Uuid.CreateVersion4(), "Author", now);
            await MappingSeeds.ReviewMappingAsync(Provider, TenantId, ProgramId, ControlId, edition, "CC6.1",
                mappingId, 1, "accept", "Ok.", Uuid.CreateVersion4(), "Reviewer", now);
            return mappingId;
        }

        public async Task<Uuid> SeedAcceptedRiskTreatmentAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var riskId = Uuid.CreateVersion4();
            var treatmentId = Uuid.CreateVersion4();
            var proposer = Uuid.CreateVersion4();
            var reviewer = Uuid.CreateVersion4();
            await RiskSeeds.ProposeTreatmentAsync(Provider, TenantId, ProgramId, riskId, treatmentId,
                ControlId, InitialVersionId, "Treats the risk.", proposer,
                ActorReference.ForMember(proposer, "Proposer"), now);
            await RiskSeeds.ReviewTreatmentAsync(Provider, TenantId, ProgramId, riskId, ControlId,
                treatmentId, "accept", "Ok.", ActorReference.ForMember(reviewer, "Reviewer"), now);
            return treatmentId;
        }

        public async Task<Uuid> SeedPersonAsync()
        {
            var personId = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(Provider, new PersonRules(TenantId, personId),
                person => person.Record("Facilities manager", "facilities@example.com",
                    ActorReference.ForMember(Uuid.CreateVersion4(), "Admin"),
                    DateTimeOffset.UtcNow));
            return personId;
        }

        public async Task<Uuid> SeedEvidenceRequestAsync(Uuid? artifactId = null,
            bool cancel = false, Uuid? tenantId = null, Uuid? programId = null,
            Uuid? controlId = null)
        {
            var requestId = Uuid.CreateVersion4();
            var now = DateTimeOffset.UtcNow;
            var actor = ActorReference.ForMember(OwnerMemberId, "Owner");
            var tenant = tenantId ?? TenantId;
            var program = programId ?? ProgramId;
            await EvidenceSeeds.OpenAsync(Provider, tenant, program, requestId, "Access review evidence",
                "Retain the review record.", OwnerMemberId, DateOnly.FromDateTime(now.UtcDateTime).AddDays(30),
                controlId ?? ControlId, actor, now);
            if (artifactId is { } artifact)
                await EvidenceSeeds.FulfilAsync(Provider, tenant, program, requestId, artifact, actor, now);
            else if (cancel)
                await EvidenceSeeds.CancelAsync(Provider, tenant, program, requestId, "Not required.", actor, now);
            return requestId;
        }

        public Task CancelEvidenceRequestAsync(Uuid requestId) =>
            EvidenceSeeds.CancelAsync(Provider, TenantId, ProgramId, requestId, "No longer required.",
                ActorReference.ForMember(OwnerMemberId, "Owner"), DateTimeOffset.UtcNow);

        public async Task<Uuid> SeedOperatingPlanAsync(bool approve = true,
            DateOnly? effectiveFrom = null)
        {
            var planId = Uuid.CreateVersion4();
            var control = await ProgramManagementServices.HydrateAsync(Provider,
                new ControlRules(TenantId, ControlId));
            var now = DateTimeOffset.UtcNow;
            await ControlOperatingSeed.ProposeAsync(Provider, TenantId, ProgramId, ControlId,
                control.ApprovedVersion!.VersionId, control.ApprovedVersion.Content.ExpectedEvidenceDescriptions, planId, new OperatingHolder("member", OwnerMemberId), null,
                RbacIds.Member(TenantId, ReviewerUserId), new ControlCadence("ad_hoc", DueWithinDays: 1),
                effectiveFrom ?? EffectiveFrom, "Operate when triggered.",
                RbacIds.Member(TenantId, AuthorUserId), "Author", now);
            if (approve)
                await ControlOperatingSeed.ApproveAsync(Provider, TenantId, ProgramId, ControlId, planId,
                    RbacIds.Member(TenantId, ApproverUserId), "Approver", now);
            return planId;
        }

        public async Task<Uuid> SeedOccurrenceAsync(bool complete = false,
            Uuid? artifactId = null, string? evidenceReference = null)
        {
            var now = DateTimeOffset.UtcNow;
            var plan = await ControlOperatingSeed.CurrentPlanAsync(Provider, TenantId, ProgramId, ControlId);
            var opened = await ControlOperatingSeed.OpenAsync(Provider, TenantId, ProgramId, ControlId, plan,
                new ControlEventWindow("test", Uuid.CreateVersion4().ToString()), "Review access.",
                DateOnly.FromDateTime(now.UtcDateTime), OwnerMemberId, "Owner", now);
            if (!complete && artifactId is null && evidenceReference is null)
                return opened.OccurrenceId;
            var attested = await ControlOperatingSeed.AttestAsync(Provider, opened, plan,
                new AttestationInput("complete", now, null, null, "Reviewed.", null,
                    [new EvidenceReference(0, "artifact",
                        evidenceReference ?? (artifactId ?? Uuid.CreateVersion4()).ToString())],
                    new OperatingHolder("member", OwnerMemberId)), OwnerMemberId, "Owner", now);
            if (complete)
                await ControlOperatingSeed.ReviewAsync(Provider, attested, "approved", [],
                    RbacIds.Member(TenantId, ReviewerUserId), "Reviewer", now);
            return opened.OccurrenceId;
        }

        public async Task<Uuid> SeedCorrectiveActionAsync(bool complete = false,
            Uuid? tenantId = null, Uuid? programId = null, Uuid? controlId = null,
            Uuid? artifactId = null, string? linkReference = null,
            Uuid? sourceOccurrenceId = null)
        {
            var findingId = Uuid.CreateVersion4();
            var actionId = Uuid.CreateVersion4();
            var now = DateTimeOffset.UtcNow;
            var actor = ActorReference.ForMember(OwnerMemberId, "Owner");
            var (tenant, program) = (tenantId ?? TenantId, programId ?? ProgramId);
            await FindingSeed.RaiseAsync(Provider, tenant, program, findingId,
                sourceOccurrenceId is { } sourceId
                    ? new FindingSource("control_occurrence", sourceId, null, "Access was retained.")
                    : new FindingSource("manual", null, null, "Access was retained."),
                "Stale access", "A leaver retained access.", "medium", "Accounts", OwnerMemberId,
                EffectiveFrom.AddMonths(1),
                sourceOccurrenceId is null
                    ? [new FindingLink("control", linkReference ?? (controlId ?? ControlId).ToString())]
                    : [], actor, now);
            await FindingSeed.AddActionAsync(Provider, tenant, program, findingId, actionId, "Remove access.",
                OwnerMemberId, EffectiveFrom.AddMonths(1), actor, now,
                complete
                    ? artifactId is { } artifact
                        ? new EvidenceReference(null, "artifact", artifact.ToString())
                        : new EvidenceReference(null, "external", "https://tickets.example/1")
                    : null);
            return actionId;
        }

        public async Task<Uuid> SeedEvaluationAsync(Uuid? artifactId = null)
        {
            var evaluationId = Uuid.CreateVersion4();
            await EvaluationSeeds.StartAsync(Provider, TenantId, ProgramId, ControlId, evaluationId, InitialVersionId,
                Uuid.CreateVersion4(), 1,
                [new EvaluationProcedureStep("design", "inspection",
                    [artifactId is { } artifact
                        ? new EvaluationInspectedItem("artifact", artifact.ToString(), "sha256:original")
                        : new EvaluationInspectedItem("record", "procedure", "v1")],
                    "Access review is defined.")], RbacIds.Member(TenantId, ReviewerUserId), "Reviewer",
                DateTimeOffset.UtcNow);
            return evaluationId;
        }

        public async Task<Uuid> CloseAndReopenFindingAsync(Uuid actionId, Uuid artifactId)
        {
            var closureId = Uuid.CreateVersion4();
            await using var scope = Provider.CreateAsyncScope();
            var findings = await scope.ServiceProvider.GetRequiredService<FindingReader>().ReadAsync(TenantId, ProgramId,
                DateTimeOffset.UtcNow);
            var finding = Assert.Single(findings.Value.Findings, item =>
                item.CorrectiveActions.Any(action => action.ActionId == actionId));
            await FindingSeed.CloseAsync(Provider, TenantId, ProgramId, finding.FindingId, closureId,
                RbacIds.Member(TenantId, ReviewerUserId), new EvidenceReference(null, "artifact", artifactId.ToString()),
                DateTimeOffset.UtcNow, reopen: true);
            return closureId;
        }

        public async Task<Uuid> ReviewAsync(long revision)
        {
            var requestId = Uuid.CreateVersion4();
            await PersonalControlDecisionTransportTests.HttpAsync(Provider, ReviewerUserId,
                new ReviewControl(requestId, TenantId, ProgramId, ControlId, revision, "accept", "Reviewed"),
                metadata: new RequestMetadata(requestId, requestId, null));
            return requestId;
        }

        public async Task<TOut> QueryAsync<TRequest, TOut>(TRequest request)
            where TRequest : IRequest<TOut>
        {
            var result = await Scenario(ApproverUserId).When(request).ExpectSuccess();
            return result.Value;
        }

        static Result CommandResult(CommandFailure? failure) => failure is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));
    }

    sealed class AcceptingValidator : IControlApplicabilityReferenceValidator
    {
        public ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content,
            CancellationToken ct = default) => ValueTask.FromResult(Result.Success);
    }
}
