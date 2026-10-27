using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Projections;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class ReadinessSourceRulesTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly DateTimeOffset AsOf = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordFamilyGapsGivenProgramWithoutBoundaryCommitmentRiskOrSnapshot()
    {
        // Arrange
        var sources = ReadinessSourceSet.Empty;

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_approved_boundary");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_commitments");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_risks");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_scope_snapshot");
        foreach (var family in new[] { "boundaries", "commitments", "risks", "population_snapshots" })
            Assert.Equal("assessed", Assert.Single(evaluation.Inputs, i => i.Family == family)
                .Status);
        Assert.Equal("assessed", Assert.Single(evaluation.Inputs,
            i => i.Family == "applications_access_review_scope").Status);
        Assert.Equal("assessed", Assert.Single(evaluation.Inputs,
            i => i.Family == "technology_inventory").Status);
        foreach (var family in new[] { "workforce", "evidence" })
            Assert.Contains(evaluation.Gaps, g => g.Kind == "input_not_assessed" &&
                                                   g.Subject == family);
    }

    [Fact]
    public void ShouldKeepWorkforceCompletenessExplicitGivenRosterSnapshotAvailability()
    {
        // Arrange
        var snapshot = new ReadinessWorkforceRosterSnapshotInput(Uuid.CreateVersion4(),
            new string('a', 64), AsOf.AddDays(-1), 1);
        var scenarios = new (ReadinessSourceSet Sources, string SnapshotStatus,
            string? SnapshotGapKind)[]
        {
            (ReadinessSourceSet.Empty, "assessed", "workforce_roster_snapshot_missing"),
            (ReadinessSourceSet.Empty with
            {
                WorkforceRosterSnapshot = snapshot with { RowCount = 0 },
            }, "assessed", "workforce_roster_snapshot_empty"),
            (ReadinessSourceSet.Empty with { WorkforceRosterSnapshot = snapshot },
                "assessed", null),
            (ReadinessSourceSet.Empty with
            {
                TruncatedFamilies = ["workforce_roster_snapshots"],
            }, "not_assessed", null),
        };
        const string expectedExplanation = "The rules verify only an integrity-checked workforce-roster snapshot available as of this assessment, when one exists; they do not assess workforce completeness beyond it.";

        // Act
        var evaluations = scenarios.Select(scenario => (scenario,
            Evaluation: Evaluate(scenario.Sources))).ToArray();

        // Assert
        foreach (var (scenario, evaluation) in evaluations)
        {
            var workforceInput = Assert.Single(evaluation.Inputs,
                input => input.Family == "workforce");
            var snapshotInput = Assert.Single(evaluation.Inputs,
                input => input.Family == "workforce_roster_snapshot");
            var workforceGap = Assert.Single(evaluation.Gaps,
                gap => gap.Kind == "input_not_assessed" && gap.Subject == "workforce");

            Assert.Equal("not_assessed", workforceInput.Status);
            Assert.Equal(expectedExplanation, workforceInput.Explanation);
            Assert.Equal(scenario.SnapshotStatus, snapshotInput.Status);
            Assert.Equal(expectedExplanation + " Review this gap manually.",
                workforceGap.Explanation);
            if (scenario.Sources.TruncatedFamilies.Contains("workforce_roster_snapshots"))
                Assert.Contains(evaluation.Gaps, gap => gap.Kind == "source_truncated" &&
                                                       gap.Subject == "workforce_roster_snapshots");
            if (scenario.SnapshotGapKind is { } snapshotGapKind)
                Assert.Contains(evaluation.Gaps, gap => gap.Kind == snapshotGapKind);
            else
            {
                Assert.DoesNotContain(evaluation.Gaps,
                    gap => gap.Kind == "workforce_roster_snapshot_missing");
                Assert.DoesNotContain(evaluation.Gaps,
                    gap => gap.Kind == "workforce_roster_snapshot_empty");
            }
        }
    }

    [Fact]
    public void ShouldAssessProviderAndEmitStableReviewGapGivenMaterialProviderWithoutReview()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var provider = new ProviderView(Tenant, providerId, 1,
            new ProviderContent("Cloud hosting", "hosting", Materiality: "material",
                Subservice: true, BoundaryTreatment: "carve_out"), "manual", "active", [],
            ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), AsOf.AddDays(-2));
        var sources = ReadinessSourceSet.Empty with
        {
            Providers = [new ReadinessProviderInput(provider, [], [])],
        };

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        var family = Assert.Single(evaluation.Inputs, input => input.Family == "providers");
        Assert.Equal("assessed", family.Status);
        Assert.Equal(1, family.RecordCount);
        var gap = Assert.Single(evaluation.Gaps, item =>
            item.Kind == "provider_review_incomplete" && item.Subject == providerId.ToString());
        Assert.Equal(ReadinessRules.GapIdFor(Program, ReadinessRules.ProviderReviewCurrent,
            providerId.ToString()), gap.GapId);
        Assert.DoesNotContain(evaluation.Gaps, item => item.Kind == "input_not_assessed" &&
            item.Subject == "providers");
    }

    [Fact]
    public void ShouldLinkUnresolvedProviderCoverageGapToReadinessGapGivenAssessmentAsOfTime()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var sourceGapId = Uuid.CreateVersion4();
        var resolvedGapId = Uuid.CreateVersion4();
        var provider = new ProviderView(Tenant, providerId, 2,
            new ProviderContent("Cloud hosting", "hosting", Materiality: "material"), "manual", "active", [],
            ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), AsOf.AddDays(-2));
        var sources = ReadinessSourceSet.Empty with
        {
            Providers = [new ReadinessProviderInput(provider, [], [],
            [
                CoverageGap(providerId, sourceGapId, 2, "closed", AsOf.AddDays(-1), AsOf.AddDays(1)),
                CoverageGap(providerId, resolvedGapId, 2, "closed", AsOf.AddDays(-2), AsOf.AddHours(-1)),
            ])],
        };

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        var linkedGap = Assert.Single(evaluation.Gaps, gap => gap.Kind == "provider_coverage_gap_unresolved");
        Assert.Equal(ReadinessRules.GapIdFor(Program, ReadinessRules.ProviderCoverageGapUnresolved,
            sourceGapId.ToString()), linkedGap.GapId);
        Assert.Contains(linkedGap.Sources, source => source.Kind == "provider_coverage_gap" &&
            source.Id == sourceGapId && source.Version == "1");
        Assert.DoesNotContain(evaluation.Gaps, gap => gap.Subject == resolvedGapId.ToString());
    }

    [Fact]
    public void ShouldKeepProviderGapOpenAndRedactRestrictedDetailsGivenAcceptedExposure()
    {
        // Arrange
        var providerId = Uuid.CreateVersion4();
        var sourceGapId = Uuid.CreateVersion4();
        var riskId = Uuid.CreateVersion4();
        var acceptanceId = Uuid.CreateVersion4();
        var acceptedUntil = AsOf.AddDays(30);
        const string restrictedDescription = "Private audit detail about the provider control gap.";
        var provider = new ProviderView(Tenant, providerId, 1,
            new ProviderContent("Cloud hosting", "hosting", Materiality: "material"), "manual", "active", [],
            ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), AsOf.AddDays(-2));
        var acceptance = new ProviderCoverageGapRiskAcceptanceView(Program, riskId,
            acceptanceId, acceptedUntil, ActorReference.ForMember(Uuid.CreateVersion4(), "Jordan Approver"),
            AsOf.AddDays(-1))
        { Revision = 2 };
        var sourceGap = CoverageGap(providerId, sourceGapId, 2, "open", AsOf.AddDays(-2), null);
        var gap = sourceGap with
        {
            Content = sourceGap.Content with
            {
                Description = restrictedDescription,
            },
            RiskAcceptances = [acceptance],
        };
        var sources = ReadinessSourceSet.Empty with
        {
            Providers = [new ReadinessProviderInput(provider, [], [], [gap])],
        };

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        var linkedGap = Assert.Single(evaluation.Gaps, item => item.Kind == "provider_coverage_gap_unresolved");
        Assert.Contains(linkedGap.Sources, source => source.Kind == "provider_coverage_gap" &&
            source.Id == sourceGapId && source.Version == "2");
        Assert.DoesNotContain(linkedGap.Sources, source => source.Kind is "risk" or "risk_acceptance");
        Assert.DoesNotContain(acceptanceId.ToString(), linkedGap.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain(riskId.ToString(), linkedGap.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain(restrictedDescription, linkedGap.Explanation, StringComparison.Ordinal);
        Assert.Contains("remains uncovered", linkedGap.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldMeetFamilyRulesGivenApprovedBoundaryEffectiveCommitmentTreatedRiskAndSnapshot()
    {
        // Arrange
        var sources = new ReadinessSourceSet(
            [Boundary((AsOf.AddDays(-2), AsOf.AddDays(-2)))],
            [Commitment(AsOf.AddDays(-10), AsOf.AddDays(-3))],
            [Risk(AsOf.AddDays(-10), "accepted")], [Snapshot(AsOf.AddHours(-1))]);

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.DoesNotContain(evaluation.Gaps, g => g.RuleId is ReadinessRules.BoundaryApproved
            or ReadinessRules.CommitmentEffective or ReadinessRules.RiskResolved
            or ReadinessRules.ScopeSnapshotFrozen);
    }

    [Fact]
    public void ShouldRecordPerRecordGapsGivenUnapprovedCommitmentUnassessedRiskAndLaterSources()
    {
        // Arrange
        var risk = Risk(AsOf.AddDays(-5), "treatment_chosen");
        var sources = new ReadinessSourceSet([Boundary((AsOf.AddDays(1), AsOf.AddDays(-1)))],
            [Commitment(AsOf.AddDays(-5), null)], [risk], [Snapshot(AsOf.AddHours(1))]);

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_approved_boundary");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "commitment_not_effective" &&
                                               g.Subject == "C-1");
        var riskGap = Assert.Single(evaluation.Gaps, g => g.Kind == "risk_unresolved");
        Assert.Equal(risk.RiskId, Assert.Single(riskGap.Sources).Id);
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_scope_snapshot");
    }

    [Fact]
    public void ShouldIgnoreLaterApprovalAndLaterRiskGivenAssessmentAsOfLastMonth()
    {
        // Arrange
        var lastMonth = AsOf.AddMonths(-1);
        var commitment = Commitment(AsOf.AddMonths(-2), AsOf.AddDays(-1));
        var laterRisk = Risk(AsOf.AddDays(-2), "accepted");
        var sources = new ReadinessSourceSet([], [commitment], [laterRisk], []);

        // Act
        var evaluation = Evaluate(sources, lastMonth);

        // Assert
        Assert.Contains(evaluation.Gaps, g => g.Kind == "commitment_not_effective" &&
                                               g.Sources[0].Version == "0");
        Assert.Contains(evaluation.Gaps, g => g.Kind == "no_risks");
        Assert.Equal(0, Assert.Single(evaluation.Inputs, i => i.Family == "risks").RecordCount);
    }

    [Fact]
    public void ShouldUseEarlierApprovedBoundaryGivenLatestApprovalAfterAsOf()
    {
        // Arrange
        var boundary = Boundary((AsOf.AddDays(-30), AsOf.AddDays(-30)),
            (AsOf.AddDays(1), AsOf.AddDays(1)));

        // Act
        var evaluation = Evaluate(new ReadinessSourceSet([boundary], [], [], []));

        // Assert
        Assert.DoesNotContain(evaluation.Gaps, g => g.Kind == "no_approved_boundary");
        Assert.Equal(1, Assert.Single(evaluation.Inputs, i => i.Family == "boundaries")
            .RecordCount);
    }

    [Fact]
    public void ShouldRecordTruncationGapGivenFamilyExceedsScanLimit()
    {
        // Arrange
        var sources = ReadinessSourceSet.Empty with { TruncatedFamilies = ["risks"] };

        // Act
        var evaluation = Evaluate(sources);

        // Assert
        Assert.Contains(evaluation.Gaps, g => g.Kind == "source_truncated" &&
                                               g.Subject == "risks");
    }

    [Fact]
    public void ShouldChangeFingerprintGivenApprovalInForce()
    {
        // Arrange
        var unapproved = new ReadinessSourceSet([], [Commitment(AsOf.AddDays(-9), null)], [],
            []);
        var approved = new ReadinessSourceSet([],
            [Commitment(AsOf.AddDays(-9), AsOf.AddDays(-1))], [], []);

        // Act
        var before = Evaluate(unapproved);
        var after = Evaluate(approved);

        // Assert
        Assert.NotEqual(before.InputFingerprint, after.InputFingerprint);
    }

    [Fact]
    public void ShouldChangeFingerprintGivenEffectiveAccessReviewScopeAttributionChanges()
    {
        // Arrange
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var decision = new AccessReviewScopeDecisionView(Tenant, applicationId,
            systemInstanceId, 3, Uuid.CreateVersion4(), 4, "included", "Quarterly review",
            AsOf.AddDays(-20), AsOf.AddDays(30),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Reviewer"),
            AsOf.AddDays(-20), Uuid.CreateVersion4());

        // Act
        var original = Evaluate(AccessReviewSources(decision));
        var revisions = new[]
        {
            decision with { TenantId = Uuid.CreateVersion4() },
            decision with { ApplicationId = Uuid.CreateVersion4() },
            decision with { SystemInstanceId = Uuid.CreateVersion4() },
            decision with { SystemInstanceRevision = 4 },
            decision with { DecisionId = Uuid.CreateVersion4() },
            decision with { Sequence = 5 },
            decision with { Decision = "excluded" },
            decision with { Reason = "Annual review" },
            decision with { EffectiveFrom = AsOf.AddDays(-19) },
            decision with { ReviewBy = AsOf.AddDays(31) },
            decision with { ApprovedBy = decision.ApprovedBy with { Kind = "firm_staff" } },
            decision with { ApprovedBy = decision.ApprovedBy with { Id = Uuid.CreateVersion4().ToString() } },
            decision with { ApprovedBy = decision.ApprovedBy with { Display = "Jordan Reviewer" } },
            decision with { DecidedAt = AsOf.AddDays(-19) },
            decision with { SeparationOfDutiesWaiverId = Uuid.CreateVersion4() },
        };
        var changed = revisions.Select(revision => Evaluate(AccessReviewSources(revision)))
            .ToArray();
        var sameInstantDifferentOffset = Evaluate(AccessReviewSources(decision with
        {
            EffectiveFrom = decision.EffectiveFrom.ToOffset(TimeSpan.FromHours(-5)),
        }));

        // Assert
        Assert.All(changed, evaluation =>
            Assert.NotEqual(original.InputFingerprint, evaluation.InputFingerprint));
        Assert.Equal(original.InputFingerprint, sameInstantDifferentOffset.InputFingerprint);
    }

    [Fact]
    public void ShouldSnapshotEffectiveAccessReviewScopeGivenAsOfBoundaryAndTruncatedPopulation()
    {
        // Arrange
        var applicationId = Uuid.CreateVersion4();
        var includedId = Uuid.CreateVersion4();
        var excludedId = Uuid.CreateVersion4();
        var unresolvedId = Uuid.CreateVersion4();
        var missingScopeId = Uuid.CreateVersion4();
        var effective = ScopeDecision(applicationId, includedId, 2, "included",
            AsOf.AddDays(-5), AsOf, AsOf);
        var earlier = ScopeDecision(applicationId, includedId, 1, "excluded",
            AsOf.AddDays(-10), AsOf.AddDays(-1), AsOf.AddDays(-10));
        var futureEffective = ScopeDecision(applicationId, includedId, 3, "excluded",
            AsOf.AddTicks(1), null, AsOf.AddDays(-1));
        var futureDecision = ScopeDecision(applicationId, includedId, 4, "excluded",
            AsOf.AddDays(-1), null, AsOf.AddTicks(1));
        var mismatchedApplication = ScopeDecision(Uuid.CreateVersion4(), includedId, 5,
            "excluded", AsOf.AddDays(-1), null, AsOf.AddDays(-1));
        var mismatchedTenant = ScopeDecision(applicationId, includedId, 6, "excluded",
            AsOf.AddDays(-1), null, AsOf.AddDays(-1)) with
        { TenantId = Uuid.CreateVersion4() };
        var mismatchedSystemInstance = ScopeDecision(applicationId, includedId, 7, "excluded",
            AsOf.AddDays(-1), null, AsOf.AddDays(-1)) with
        {
            SystemInstanceId = Uuid.CreateVersion4(),
        };
        var invalidRevision = ScopeDecision(applicationId, includedId, 8, "excluded",
            AsOf.AddDays(-1), null, AsOf.AddDays(-1)) with
        { SystemInstanceRevision = 0 };
        var excluded = ScopeDecision(applicationId, excludedId, 1, "excluded",
            AsOf.AddDays(-2), AsOf.AddDays(1), AsOf.AddDays(-2));
        var unresolvedFuture = ScopeDecision(applicationId, unresolvedId, 1, "included",
            AsOf.AddDays(1), null, AsOf.AddDays(-1));
        var inputs = new[]
        {
            ScopeInput(applicationId, unresolvedId, unresolvedFuture),
            ScopeInput(applicationId, excludedId, excluded),
            ScopeInput(applicationId, includedId, futureEffective, effective, earlier,
                futureDecision, mismatchedApplication, mismatchedTenant,
                mismatchedSystemInstance, invalidRevision),
            new ReadinessAccessReviewScopeInput(missingScopeId, null),
        };
        var sources = ReadinessSourceSet.Empty with
        {
            AccessReviewScopes = inputs,
            TruncatedFamilies = ["applications_access_review_scope"],
        };

        // Act
        var evaluation = Evaluate(sources);
        var reordered = Evaluate(sources with { AccessReviewScopes = inputs.Reverse().ToArray() });

        // Assert
        var rows = evaluation.AccessReviewScopes;
        Assert.Equal(inputs.Select(input => input.SystemInstanceId.ToString())
            .Order(StringComparer.Ordinal), rows.Select(row => row.SystemInstanceId.ToString()));
        var includedRow = Assert.Single(rows, row => row.SystemInstanceId == includedId);
        Assert.Equal("included", includedRow.Status);
        Assert.True(includedRow.ReviewOverdue);
        Assert.Equal(effective, includedRow.EffectiveDecision);
        var excludedRow = Assert.Single(rows, row => row.SystemInstanceId == excludedId);
        Assert.Equal("excluded", excludedRow.Status);
        Assert.False(excludedRow.ReviewOverdue);
        Assert.Equal(excluded, excludedRow.EffectiveDecision);
        var unresolvedRow = Assert.Single(rows, row => row.SystemInstanceId == unresolvedId);
        Assert.Equal("unresolved", unresolvedRow.Status);
        Assert.False(unresolvedRow.ReviewOverdue);
        Assert.Null(unresolvedRow.EffectiveDecision);
        var missingScopeRow = Assert.Single(rows, row => row.SystemInstanceId == missingScopeId);
        Assert.Equal("unresolved", missingScopeRow.Status);
        Assert.False(missingScopeRow.ReviewOverdue);
        Assert.Null(missingScopeRow.EffectiveDecision);
        Assert.Contains(evaluation.Gaps, gap => gap.Kind == "access_review_scope_unresolved" &&
            gap.Subject == unresolvedId.ToString());
        Assert.Contains(evaluation.Gaps, gap => gap.Kind == "access_review_scope_overdue" &&
            gap.Subject == includedId.ToString());
        Assert.DoesNotContain(evaluation.Gaps, gap => gap.Subject == excludedId.ToString() &&
            gap.RuleId is ReadinessRules.AccessReviewScopeResolved or
                ReadinessRules.AccessReviewScopeCurrent);
        Assert.Contains(evaluation.Gaps, gap => gap.Kind == "source_truncated" &&
            gap.Subject == "applications_access_review_scope");
        Assert.Equal(rows, reordered.AccessReviewScopes);
        Assert.Equal(evaluation.InputFingerprint, reordered.InputFingerprint);
    }

    static ReadinessEvaluation Evaluate(ReadinessSourceSet sources, DateTimeOffset? asOf = null) =>
        ReadinessRules.Evaluate(Program, asOf ?? AsOf, null, [], [],
            new Dictionary<Uuid, ControlVersionView?>(), sources);

    static ReadinessSourceSet AccessReviewSources(AccessReviewScopeDecisionView decision) =>
        ReadinessSourceSet.Empty with
        {
            AccessReviewScopes = [new ReadinessAccessReviewScopeInput(decision.SystemInstanceId,
                new AccessReviewScopeRecord(decision.TenantId, decision.ApplicationId,
                    decision.SystemInstanceId, [decision]))],
        };

    static ReadinessAccessReviewScopeInput ScopeInput(Uuid applicationId,
        Uuid systemInstanceId, params AccessReviewScopeDecisionView[] decisions) =>
        new(systemInstanceId, new AccessReviewScopeRecord(Tenant, applicationId,
            systemInstanceId, decisions));

    static AccessReviewScopeDecisionView ScopeDecision(Uuid applicationId,
        Uuid systemInstanceId, long sequence, string decision, DateTimeOffset effectiveFrom,
        DateTimeOffset? reviewBy, DateTimeOffset decidedAt) =>
        new(Tenant, applicationId, systemInstanceId, 2, Uuid.CreateVersion4(), sequence,
            decision, $"decision {sequence}", effectiveFrom, reviewBy,
            ActorReference.ForMember(Uuid.CreateVersion4(), $"Reviewer {sequence}"), decidedAt,
            Uuid.CreateVersion4());

    static ReadinessBoundaryInput Boundary(
        params (DateTimeOffset EffectiveFrom, DateTimeOffset ApprovedAt)[] approvals)
    {
        var boundaryId = Uuid.CreateVersion4();
        var versions = new List<BoundaryVersionView>();
        var decisions = new List<BoundaryDecisionView>();
        foreach (var (effectiveFrom, approvedAt) in approvals)
        {
            var versionId = Uuid.CreateVersion4();
            versions.Add(new BoundaryVersionView(Tenant, boundaryId, Program, versionId,
                versions.Count + 1, null!, "approved",
                DateOnly.FromDateTime(effectiveFrom.UtcDateTime), Uuid.CreateVersion4(),
                "Author", approvedAt.AddDays(-1)));
            decisions.Add(new BoundaryDecisionView(Tenant, boundaryId, Uuid.CreateVersion4(),
                versionId, versions.Count, "approve", Uuid.CreateVersion4(), "Approver", "Ok",
                approvedAt, null, null, null));
        }
        return new ReadinessBoundaryInput(boundaryId, versions, decisions);
    }

    static ReadinessCommitmentInput Commitment(DateTimeOffset createdAt,
        DateTimeOffset? approvedAt)
    {
        var draftId = Uuid.CreateVersion4();
        IReadOnlyList<CommitmentVersionView> versions = approvedAt is { } at
            ?
            [
                new CommitmentVersionView(Tenant, Program, draftId, Uuid.CreateVersion4(),
                    "service_commitment", "C-1", 1, 2, "Statement", "Context", "Source",
                    "Owner", "applicable", "Interpretation", null, "organization", true,
                    DateOnly.FromDateTime(at.UtcDateTime),
                    new CommitmentDecisionView(Tenant, Program, draftId, Uuid.CreateVersion4(),
                        2, "approve", null, null, null, null, "Ok", 1,
                        DateOnly.FromDateTime(at.UtcDateTime), null, Uuid.CreateVersion4(),
                        "Approver", at, null)),
            ]
            : [];
        return new ReadinessCommitmentInput(draftId, "C-1", createdAt, versions);
    }

    static ReadinessRiskInput Risk(DateTimeOffset createdAt, string status) =>
        new(Uuid.CreateVersion4(), "R-1", createdAt, 1, status);

    static ProviderCoverageGapView CoverageGap(Uuid providerId, Uuid gapId, long revision,
        string status, DateTimeOffset recordedAt, DateTimeOffset? closedAt) => new(Tenant,
        gapId, providerId, 1, revision,
        new ProviderCoverageGapContent(Uuid.CreateVersion4(), "Payroll processing", "Availability",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "assurance_report",
            Uuid.CreateVersion4(), 1, "Coverage does not meet the period."), status,
        ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), recordedAt,
        closedAt is { } at
            ? new ProviderCoverageGapClosureView(new ProviderCoverageGapClosureContent(
                "coverage_restored", "assurance_report", Uuid.CreateVersion4(), 2,
                "Later report restores coverage."), ActorReference.ForMember(Uuid.CreateVersion4(), "Avery Author"), at)
            {
                Revision = revision,
            }
            : null, []);

    static SnapshotView Snapshot(DateTimeOffset frozenAt)
    {
        var id = Uuid.CreateVersion4();
        return new SnapshotView(Tenant, id, id, null, Program, "program_scope", 1, null!, "{}",
            new string('a', 64), null, Uuid.CreateVersion4(), "Author", frozenAt);
    }
}
