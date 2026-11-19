using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class EvidenceGovernanceTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid ArtifactId = Uuid.CreateVersion4();
    static readonly Uuid DisclosureId = Uuid.CreateVersion4();
    static readonly Uuid EngagementId = Uuid.CreateVersion4();
    static readonly Uuid SupportId = Uuid.CreateVersion4();
    static readonly Uuid UnmatchedSupportId = Uuid.CreateVersion4();
    static readonly Uuid LaterSupportId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly Uuid LaterDownloadRequestId = Uuid.CreateVersion4();
    static readonly DateOnly TypeIDate = new(2026, 1, 31);
    static readonly DateTimeOffset BeforeTypeIDate = new(2026, 1, 30, 12, 0, 0,
        TimeSpan.Zero);
    static readonly DateTimeOffset AfterTypeIDate = new(2026, 2, 1, 0, 0, 0,
        TimeSpan.Zero);
    static readonly string ContentSha256 = new('a', 64);

    [Fact]
    public async Task ShouldCaptureOnlyEvidenceSupportingAnExactInScopeBaselineVersionGivenLaterAndUnmatchedLinks()
    {
        // Arrange
        var fixture = await Fixture(includeLaterLink: true);
        var withoutLaterChanges = await Fixture(includeLaterLink: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);
        var earlierBaseline = await withoutLaterChanges.Contributor.ReadAsync(Engagement(),
            withoutLaterChanges.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(earlierBaseline.IsSuccess, earlierBaseline.Error?.Message);
        Assert.Equal("evidence_governance", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("evidence_artifact", source.Kind);
        Assert.Equal(ArtifactId, source.RecordId);
        Assert.Equal(2, source.Revision);
        Assert.Equal(source.ContentSha256, Assert.Single(earlierBaseline.Value.Sources).ContentSha256);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("handling policy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("retention basis", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldCaptureEngagementHoldAsOfTypeIDateGivenReportIssuedLater()
    {
        // Arrange
        var withLaterReport = await Fixture(includeLaterLink: false, includeHold: true,
            includeLaterReport: true);
        var withoutLaterReport = await Fixture(includeLaterLink: false, includeHold: true,
            includeLaterReport: false);

        // Act
        var laterResult = await withLaterReport.Contributor.ReadAsync(Engagement(),
            withLaterReport.Context, CancellationToken.None);
        var earlierResult = await withoutLaterReport.Contributor.ReadAsync(Engagement(),
            withoutLaterReport.Context, CancellationToken.None);

        // Assert
        Assert.True(laterResult.IsSuccess, laterResult.Error?.Message);
        Assert.True(earlierResult.IsSuccess, earlierResult.Error?.Message);
        var laterHold = Assert.Single(laterResult.Value.Sources, static source =>
            source.Kind == "artifact_engagement_hold");
        var earlierHold = Assert.Single(earlierResult.Value.Sources, static source =>
            source.Kind == "artifact_engagement_hold");
        Assert.Equal(1, laterHold.Revision);
        Assert.Equal(earlierHold.ContentSha256, laterHold.ContentSha256);
    }

    [Fact]
    public async Task ShouldKeepDisclosureDigestAtApprovalGivenDeliveryAfterTypeIDate()
    {
        // Arrange
        var withLaterDelivery = await Fixture(includeLaterLink: false,
            includeDisclosure: true, includeLaterDisclosure: true);
        var atApproval = await Fixture(includeLaterLink: false,
            includeDisclosure: true, includeLaterDisclosure: false);

        // Act
        var laterResult = await withLaterDelivery.Contributor.ReadAsync(Engagement(),
            withLaterDelivery.Context, CancellationToken.None);
        var earlierResult = await atApproval.Contributor.ReadAsync(Engagement(), atApproval.Context,
            CancellationToken.None);

        // Assert
        Assert.True(laterResult.IsSuccess, laterResult.Error?.Message);
        Assert.True(earlierResult.IsSuccess, earlierResult.Error?.Message);
        var laterArtifact = Assert.Single(laterResult.Value.Sources, static source =>
            source.Kind == "evidence_artifact");
        var earlierArtifact = Assert.Single(earlierResult.Value.Sources, static source =>
            source.Kind == "evidence_artifact");
        Assert.Equal(earlierArtifact.ContentSha256, laterArtifact.ContentSha256);
    }

    [Fact]
    public async Task ShouldIgnoreHandlingPolicyAssignedAfterTypeIDateGivenLaterArtifactRevision()
    {
        // Arrange
        var withLaterPolicy = await Fixture(includeLaterLink: false,
            includeFutureHandlingPolicy: true);
        var atTypeIDate = await Fixture(includeLaterLink: false);

        // Act
        var laterResult = await withLaterPolicy.Contributor.ReadAsync(Engagement(),
            withLaterPolicy.Context, CancellationToken.None);
        var baselineResult = await atTypeIDate.Contributor.ReadAsync(Engagement(),
            atTypeIDate.Context, CancellationToken.None);

        // Assert
        Assert.True(laterResult.IsSuccess, laterResult.Error?.Message);
        Assert.True(baselineResult.IsSuccess, baselineResult.Error?.Message);
        var laterArtifact = Assert.Single(laterResult.Value.Sources, static source =>
            source.Kind == "evidence_artifact");
        var baselineArtifact = Assert.Single(baselineResult.Value.Sources, static source =>
            source.Kind == "evidence_artifact");
        Assert.Equal(baselineArtifact.ContentSha256, laterArtifact.ContentSha256);
        Assert.Contains(laterResult.Value.Blockers,
            blocker => blocker.Contains("no handling policy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldUseHandlingPolicyStateAsOfTypeIDateGivenLaterReclassification()
    {
        // Arrange
        var withLaterReclassification = await Fixture(includeLaterLink: false,
            includePriorHandlingPolicy: true, includeFutureHandlingPolicy: true);
        var atTypeIDate = await Fixture(includeLaterLink: false,
            includePriorHandlingPolicy: true);

        // Act
        var laterResult = await withLaterReclassification.Contributor.ReadAsync(Engagement(),
            withLaterReclassification.Context, CancellationToken.None);
        var baselineResult = await atTypeIDate.Contributor.ReadAsync(Engagement(),
            atTypeIDate.Context, CancellationToken.None);

        // Assert
        Assert.True(laterResult.IsSuccess, laterResult.Error?.Message);
        Assert.True(baselineResult.IsSuccess, baselineResult.Error?.Message);
        var laterArtifact = Assert.Single(laterResult.Value.Sources, static source =>
            source.Kind == "evidence_artifact");
        var baselineArtifact = Assert.Single(baselineResult.Value.Sources, static source =>
            source.Kind == "evidence_artifact");
        Assert.Equal(baselineArtifact.ContentSha256, laterArtifact.ContentSha256);
    }

    [Fact]
    public async Task ShouldRefuseHandlingPolicyGivenReferencedArtifactHeadCommitsAfterReceiptCut()
    {
        // Arrange
        var fixture = await Fixture(includeLaterLink: false, includeFutureHandlingPolicy: true, lateArtifactReceipt: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.False(result.Error.IsTransient);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task ShouldExcludeWithdrawnSupportGivenWithdrawalCommittedAtOrBeforeReceiptCut(int receiptMilliseconds)
    {
        // Arrange
        var fixture = await Fixture(includeLaterLink: false, includeWithdrawal: true,
            withdrawalReceiptMilliseconds: receiptMilliseconds);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.DoesNotContain(result.Value.Sources, static source => source.Kind == "evidence_artifact");
        Assert.Contains(result.Value.ScopeDifferences, static difference =>
            difference.Contains("withdrawn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldPreserveExactCapturedHashGivenBackdatedWithdrawalCommittedAfterReceiptCut()
    {
        // Arrange
        var before = await Fixture(includeLaterLink: false, withdrawalReceiptMilliseconds: 1);
        var after = await Fixture(includeLaterLink: false, includeWithdrawal: true,
            withdrawalReceiptMilliseconds: 1);

        // Act
        var original = await before.Contributor.ReadAsync(Engagement(), before.Context, CancellationToken.None);
        var later = await after.Contributor.ReadAsync(Engagement(), after.Context, CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal(Assert.Single(original.Value.Sources, static source => source.Kind == "evidence_artifact"),
            Assert.Single(later.Value.Sources, static source => source.Kind == "evidence_artifact"));
        Assert.Equal(original.Value.Blockers, later.Value.Blockers);
        Assert.Equal(original.Value.ScopeDifferences, later.Value.ScopeDifferences);
    }

    [Fact]
    public async Task ShouldPreserveExactCapturedHashGivenWithdrawalOccurredAfterEffectiveDate()
    {
        // Arrange
        var before = await Fixture(includeLaterLink: false);
        var after = await Fixture(includeLaterLink: false, includeWithdrawal: true, laterWithdrawal: true);

        // Act
        var original = await before.Contributor.ReadAsync(Engagement(), before.Context, CancellationToken.None);
        var later = await after.Contributor.ReadAsync(Engagement(), after.Context, CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal(Assert.Single(original.Value.Sources, static source => source.Kind == "evidence_artifact"),
            Assert.Single(later.Value.Sources, static source => source.Kind == "evidence_artifact"));
    }

    [Fact]
    public async Task ShouldRetainArtifactAndWithdrawalHistoryGivenAnotherActiveSupportForSameArtifact()
    {
        // Arrange
        var before = await Fixture(includeLaterLink: false, secondActiveSupport: true);
        var after = await Fixture(includeLaterLink: false, includeWithdrawal: true, secondActiveSupport: true);

        // Act
        var original = await before.Contributor.ReadAsync(Engagement(), before.Context, CancellationToken.None);
        var later = await after.Contributor.ReadAsync(Engagement(), after.Context, CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        var previous = Assert.Single(original.Value.Sources, static source => source.Kind == "evidence_artifact");
        var current = Assert.Single(later.Value.Sources, static source => source.Kind == "evidence_artifact");
        Assert.Equal(previous.RecordId, current.RecordId);
        Assert.Equal(previous.Revision, current.Revision);
        Assert.NotEqual(previous.ContentSha256, current.ContentSha256);
        Assert.Contains(later.Value.ScopeDifferences, static difference =>
            difference.Contains("withdrawn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldBindWithdrawalReasonIntoCapturedHistoryGivenAnotherActiveSupportForSameArtifact()
    {
        // Arrange
        var first = await Fixture(includeLaterLink: false, includeWithdrawal: true, secondActiveSupport: true);
        var second = await Fixture(includeLaterLink: false, includeWithdrawal: true, secondActiveSupport: true,
            withdrawalReason: "The artifact supports a different control.");

        // Act
        var firstResult = await first.Contributor.ReadAsync(Engagement(), first.Context, CancellationToken.None);
        var secondResult = await second.Contributor.ReadAsync(Engagement(), second.Context, CancellationToken.None);

        // Assert
        Assert.True(firstResult.IsSuccess, firstResult.Error?.Message);
        Assert.True(secondResult.IsSuccess, secondResult.Error?.Message);
        Assert.NotEqual(Assert.Single(firstResult.Value.Sources, static source => source.Kind == "evidence_artifact")
                .ContentSha256,
            Assert.Single(secondResult.Value.Sources, static source => source.Kind == "evidence_artifact")
                .ContentSha256);
    }

    [Fact]
    public async Task ShouldRetainChronologicalHistoryGivenLinkAndWithdrawalAtSameInstant()
    {
        // Arrange
        var fixture = await Fixture(includeLaterLink: false, includeWithdrawal: true,
            secondActiveSupport: true, sameTimeWithdrawal: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.Sources, static source => source.Kind == "evidence_artifact");
        Assert.Contains(result.Value.ScopeDifferences, static difference =>
            difference.Contains("withdrawn", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("missing_link")]
    [InlineData("duplicate_link")]
    [InlineData("duplicate_withdrawal")]
    [InlineData("wrong_program")]
    [InlineData("wrong_subject")]
    [InlineData("wrong_tenant")]
    [InlineData("wrong_revision")]
    [InlineData("wrong_expected_revision")]
    [InlineData("empty_command")]
    [InlineData("empty_actor")]
    [InlineData("empty_reason")]
    [InlineData("untrimmed_reason")]
    [InlineData("oversized_reason")]
    [InlineData("oversized_actor_display")]
    [InlineData("invalid_digest")]
    [InlineData("earlier_occurrence")]
    [InlineData("wrong_physical_offset")]
    [InlineData("wrong_metadata_version")]
    [InlineData("wrong_metadata_time")]
    [InlineData("wrong_metadata_identity")]
    [InlineData("duplicate_metadata_event")]
    [InlineData("audit_metadata")]
    [InlineData("unknown_event")]
    public async Task ShouldRefuseFullSupportHistoryGivenMalformedPostCutWithdrawalSuffix(string fault)
    {
        // Arrange
        var fixture = await Fixture(includeLaterLink: false, includeWithdrawal: true,
            withdrawalReceiptMilliseconds: 1, supportHistoryFault: fault);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.False(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRefuseTransientlyGivenWithdrawalAppearsBetweenFullHistoryReads()
    {
        // Arrange
        var fixture = await Fixture(includeLaterLink: false, includeWithdrawal: true,
            supportHistoryFault: "between_reads");

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    static async Task<FixtureState> Fixture(bool includeLaterLink, bool includeHold = false,
        bool includeLaterReport = false, bool includeDisclosure = false,
        bool includeLaterDisclosure = false, bool includePriorHandlingPolicy = false,
        bool includeFutureHandlingPolicy = false, bool lateArtifactReceipt = false,
        bool includeWithdrawal = false, bool laterWithdrawal = false, bool secondActiveSupport = false,
        bool sameTimeWithdrawal = false,
        int? withdrawalReceiptMilliseconds = null, string? supportHistoryFault = null,
        string withdrawalReason = "The artifact was linked by mistake.")
    {
        var store = new InMemoryEventStore();
        var matching = Support(SupportId, ControlId, 2, BeforeTypeIDate);
        var unmatched = Support(UnmatchedSupportId, secondActiveSupport ? ControlId : Uuid.CreateVersion4(), 2,
            BeforeTypeIDate.AddMinutes(1));
        var links = new List<EvidenceSupportLinked> { matching, unmatched };
        if (includeLaterLink)
            links.Add(Support(LaterSupportId, ControlId, 2, AfterTypeIDate));
        foreach (var support in links)
        {
            var attached = DomainEventSeed.Attach(support, support.SubjectId, 1,
                eventId: Uuid.CreateVersion5(support.SubjectId, "evidence_support_linked_v1"),
                occurredOn: support.OccurredAt);
            await store.AppendAsync(new EventStreamAddress(TenantId.ToString(), "evidence-support",
                support.SubjectId.ToString()), 0, [attached]);
        }

        if (includeWithdrawal)
        {
            var withdrawnAt = laterWithdrawal ? AfterTypeIDate : sameTimeWithdrawal
                ? BeforeTypeIDate : BeforeTypeIDate.AddMinutes(2);
            var withdrawal = new EvidenceSupportWithdrawn(TenantId, SupportId, 2, MemberId,
                withdrawnAt, ProgramId, Uuid.CreateVersion5(SupportId, "withdrawal_command"), 1,
                new string('c', 64), withdrawalReason, "Compliance Lead");
            await store.AppendAsync(new EventStreamAddress(TenantId.ToString(), "evidence-support",
                SupportId.ToString()), 1, [DomainEventSeed.Attach(withdrawal, SupportId, 2,
                    eventId: Uuid.CreateVersion5(SupportId, "evidence_support_withdrawn_v1"),
                    occurredOn: withdrawnAt)]);
        }

        var registeredAt = BeforeTypeIDate.AddHours(-2);
        var registration = new EvidenceArtifactRegistered(TenantId, ArtifactId,
            new EvidenceArtifactContent("Control evidence", null, "document", "client",
                registeredAt, TypeIDate, TypeIDate, "internal"), ContentSha256, 123,
            ActorReference.ForMember(MemberId, "Evidence custodian"), registeredAt);
        var inspectedAt = BeforeTypeIDate.AddHours(-1);
        var inspection = new EvidenceArtifactInspected(TenantId, ArtifactId,
            EvidenceArtifactStates.Available, null, inspectedAt);
        var artifactEventList = new List<DomainEvent>
        {
            DomainEventSeed.Attach(registration, ArtifactId, 1,
                eventId: Uuid.CreateVersion5(ArtifactId, "registered"), occurredOn: registeredAt),
            DomainEventSeed.Attach(inspection, ArtifactId, 2,
                eventId: Uuid.CreateVersion5(ArtifactId, "inspected"), occurredOn: inspectedAt),
        };
        if (includeFutureHandlingPolicy)
        {
            var download = new EvidenceArtifactDownloadIssued(TenantId, ArtifactId, 3,
                LaterDownloadRequestId, ContentSha256, 123, "Later examination request",
                ActorReference.ForMember(MemberId, "Evidence custodian"), AfterTypeIDate);
            artifactEventList.Add(DomainEventSeed.Attach(download, ArtifactId, 3,
                eventId: Uuid.CreateVersion5(ArtifactId, "later_download"),
                occurredOn: AfterTypeIDate));
        }
        var artifactEvents = artifactEventList.ToArray();
        var artifactStream = new EventStreamAddress(TenantId.ToString(), "evidence-artifacts",
            ArtifactId.ToString());
        await store.AppendAsync(artifactStream, 0, artifactEvents);
        var artifact = new AggregateScenario<EvidenceArtifact>(new EvidenceArtifact(TenantId,
            ArtifactId)).Given(artifactEvents).Aggregate;

        var fixtureAggregates = new List<Aggregate> { artifact };
        if (includeFutureHandlingPolicy || includePriorHandlingPolicy)
        {
            var policySource = new ArtifactRetentionSource(TenantId, "evidence_artifact",
                ArtifactId, ContentSha256);
            var policyActor = ActorReference.ForMember(MemberId, "Evidence custodian");
            var policyId = EvidenceHandlingPolicy.IdFor(TenantId, ArtifactId);
            var policyEvents = new List<DomainEvent>();
            var history = new List<EvidenceHandlingPolicyHistoryEntry>();
            if (includePriorHandlingPolicy)
            {
                history.Add(new EvidenceHandlingPolicyHistoryEntry(1, "confidential", 1,
                    "Type I data classification", policyActor, BeforeTypeIDate));
                var assignedState = new EvidenceHandlingPolicyView(TenantId, ArtifactId,
                    policySource, 2, 123, EvidenceArtifactStates.Available, "internal",
                    "confidential", 1, 1, history[0].Rationale, policyActor, BeforeTypeIDate,
                    Array.AsReadOnly(history.ToArray()));
                var assigned = new ArtifactHandlingPolicyAssigned(TenantId, ArtifactId, 1,
                    MemberId, assignedState, BeforeTypeIDate);
                policyEvents.Add(DomainEventSeed.Attach(assigned, policyId, 1,
                    eventId: Uuid.CreateVersion5(policyId, "policy_assignment_v1"),
                    occurredOn: BeforeTypeIDate));
            }
            if (includeFutureHandlingPolicy)
            {
                history.Add(new EvidenceHandlingPolicyHistoryEntry(history.Count + 1,
                    "restricted", 1, "Later policy reclassification", policyActor,
                    AfterTypeIDate));
                var revisedState = new EvidenceHandlingPolicyView(TenantId, ArtifactId,
                    policySource, 3, 123, EvidenceArtifactStates.Available, "internal",
                    "restricted", 1, history.Count, history[^1].Rationale, policyActor,
                    AfterTypeIDate, Array.AsReadOnly(history.ToArray()));
                DomainEvent revised = history.Count == 1
                    ? new ArtifactHandlingPolicyAssigned(TenantId, ArtifactId, 1, MemberId,
                        revisedState, AfterTypeIDate)
                    : new ArtifactHandlingPolicyRevised(TenantId, ArtifactId, history.Count,
                        MemberId, revisedState, AfterTypeIDate);
                policyEvents.Add(DomainEventSeed.Attach(revised, policyId, (ulong)history.Count,
                    eventId: Uuid.CreateVersion5(policyId,
                        $"policy_assignment_v1:{history.Count}"), occurredOn: AfterTypeIDate));
            }
            var attachedPolicyEvents = policyEvents.ToArray();
            await store.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "evidence-handling-policies", policyId.ToString()), 0, attachedPolicyEvents);
            fixtureAggregates.Add(new AggregateScenario<EvidenceHandlingPolicy>(
                new EvidenceHandlingPolicy(TenantId, ArtifactId)).Given(attachedPolicyEvents).Aggregate);
        }
        if (includeDisclosure)
        {
            var actor = ActorReference.ForMember(MemberId, "Evidence custodian");
            var source = new ArtifactRetentionSource(TenantId, "evidence_artifact", ArtifactId,
                ContentSha256);
            var approved = new ArtifactDisclosureView(TenantId, DisclosureId, ArtifactId, source,
                123, "internal", 1, 2, "External auditor", "Type I examination",
                "Approved evidence for examination.", actor, BeforeTypeIDate, "approved", 1,
                null, null, null, null, false);
            var disclosureHistory = new List<DomainEvent>
            {
                new ArtifactDisclosureApproved(TenantId, DisclosureId, 1, MemberId, approved,
                    BeforeTypeIDate),
            };
            if (includeLaterDisclosure)
                disclosureHistory.Add(new ArtifactDisclosureDelivered(TenantId, DisclosureId, 2,
                    MemberId, approved with
                    {
                        Status = "delivered",
                        Revision = 2,
                        Delivery = new ArtifactDisclosureDeliveryView("receipt-1", actor,
                            AfterTypeIDate, 2),
                    }, AfterTypeIDate));
            var disclosureEvents = disclosureHistory.Select((domainEvent, index) =>
                DomainEventSeed.Attach(domainEvent, DisclosureId, (ulong)index + 1,
                    eventId: Uuid.CreateVersion5(DisclosureId,
                        $"disclosure_event_v1:{index + 1}"),
                    occurredOn: DisclosureEventAt(domainEvent))).ToArray();
            await store.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "artifact-disclosures", DisclosureId.ToString()), 0, disclosureEvents);
            fixtureAggregates.Add(new AggregateScenario<ArtifactDisclosure>(
                new ArtifactDisclosure(TenantId, DisclosureId)).Given(disclosureEvents).Aggregate);
        }
        if (includeHold)
        {
            var actor = ActorReference.ForMember(MemberId, "Evidence custodian");
            var state = new ArtifactEngagementHoldView(TenantId, EngagementId, EngagementId,
                null, "Type I evidence", "M0-D16", "Preserve evidence until report issuance.",
                "active", 1, actor, BeforeTypeIDate, null, null, null, null, null, null, null,
                null, BeforeTypeIDate);
            var pending = new List<DomainEvent>
            {
                new ArtifactEngagementHoldPlaced(TenantId, EngagementId, 1, MemberId, state,
                    BeforeTypeIDate),
            };
            if (includeLaterReport)
            {
                var reportArtifactId = Uuid.CreateVersion4();
                state = state with
                {
                    ReportArtifactId = reportArtifactId,
                    IssuerReference = "report-1",
                    ReportIssuedBy = actor,
                    ReportIssuedAt = AfterTypeIDate,
                    ReleaseEligibleOn = DateOnly.FromDateTime(AfterTypeIDate.UtcDateTime).AddYears(1),
                    Revision = 2,
                    UpdatedAt = AfterTypeIDate,
                };
                pending.Add(new EngagementReportIssued(TenantId, EngagementId, 2, MemberId,
                    state, AfterTypeIDate));
            }
            var holdEvents = pending.Select((domainEvent, index) => DomainEventSeed.Attach(
                domainEvent, EngagementId, (ulong)index + 1,
                eventId: Uuid.CreateVersion5(EngagementId, $"hold_event_v1:{index + 1}"),
                occurredOn: HoldEventAt(domainEvent))).ToArray();
            await store.AppendAsync(new EventStreamAddress(TenantId.ToString(),
                "artifact-engagement-holds", EngagementId.ToString()), 0, holdEvents);
            fixtureAggregates.Add(new AggregateScenario<ArtifactEngagementHold>(
                new ArtifactEngagementHold(TenantId, EngagementId)).Given(holdEvents).Aggregate);
        }

        IDomainEventReader eventReader = supportHistoryFault is null ? store :
            new FaultedSupportHistory(store, supportHistoryFault);
        ITypeIBaselineSourceContributor contributor =
            new EvidenceGovernanceTypeIBaselineSourceContributor(eventReader,
                new AggregateReader(fixtureAggregates.ToArray()));
        var effectiveControlVersion = new TypeIBaselineSourceReference("control_version",
            ControlId, Uuid.CreateVersion4(), 2, new string('b', 64));
        var context = new TypeIBaselineSourceReadContext(MemberId, [effectiveControlVersion]);
        if (lateArtifactReceipt || withdrawalReceiptMilliseconds is not null)
        {
            var cutoff = AfterTypeIDate.AddDays(3);
            var receipts = new ScriptedNativeEventReceipts();
            await foreach (var record in store.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()), EventCursor.Start, CancellationToken.None))
                receipts.Record(record, record.Event switch
                {
                    EvidenceSupportWithdrawn => cutoff.AddMilliseconds(withdrawalReceiptMilliseconds ?? -1),
                    EvidenceArtifactDownloadIssued when lateArtifactReceipt => cutoff.AddMilliseconds(1),
                    _ => cutoff.AddMilliseconds(-1),
                });
            context = context with
            {
                RecordedThrough = cutoff,
                ReceiptCut = new RecordedSourceReceiptCutFactory(store, receipts).Create(TenantId, cutoff)
            };
        }
        return new FixtureState(contributor, context);
    }

    static EvidenceSupportLinked Support(Uuid supportId, Uuid supportedRecordId,
        long supportedRevision, DateTimeOffset occurredAt) => new(TenantId, supportId, 1,
            MemberId, occurredAt)
        {
            ProgramId = ProgramId,
            ArtifactId = ArtifactId,
            ArtifactRevision = 2,
            ArtifactContentSha256 = ContentSha256,
            SupportedRecordType = "control_version",
            SupportedRecordId = supportedRecordId,
            SupportedRecordRevision = supportedRevision,
            PeriodStart = TypeIDate,
            PeriodEnd = TypeIDate,
            Purpose = "Test the effective control version.",
            SufficiencyRationale = "The artifact supports the selected control version.",
            ActorDisplay = "Evidence reviewer",
        };

    static DateTimeOffset HoldEventAt(DomainEvent domainEvent) => domainEvent switch
    {
        ArtifactEngagementHoldPlaced placed => placed.OccurredAt,
        EngagementReportIssued issued => issued.OccurredAt,
        _ => throw new ArgumentOutOfRangeException(nameof(domainEvent)),
    };

    static DateTimeOffset DisclosureEventAt(DomainEvent domainEvent) => domainEvent switch
    {
        ArtifactDisclosureApproved approved => approved.OccurredAt,
        ArtifactDisclosureDelivered delivered => delivered.OccurredAt,
        _ => throw new ArgumentOutOfRangeException(nameof(domainEvent)),
    };

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        EngagementId, "type_i", "Audit Firm", TypeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1,
        new DateTimeOffset(TypeIDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
        MemberId, "Examiner");

    sealed record FixtureState(ITypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class FaultedSupportHistory(IDomainEventReader inner, string fault) : IDomainEventReader
    {
        int _supportReads;

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            CancellationToken ct = default) => inner.ReadAsync(stream, fromOffset, ct);

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var supportRead = pattern.Area == "evidence-support" ? ++_supportReads : 0;
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
            {
                if (record.Stream.Area != "evidence-support" || record.Stream.Resource != SupportId.ToString())
                {
                    yield return record;
                    continue;
                }
                if (record.Event is EvidenceSupportLinked)
                {
                    if (fault == "missing_link")
                        continue;
                    yield return record;
                    if (fault == "duplicate_link")
                        yield return record;
                    continue;
                }
                if (record.Event is not EvidenceSupportWithdrawn withdrawal)
                {
                    yield return record;
                    continue;
                }
                if (fault == "between_reads" && supportRead == 1)
                    continue;
                if (fault == "wrong_physical_offset")
                {
                    yield return record with { ResourceOffset = 2 };
                    continue;
                }
                DomainEvent changed = fault switch
                {
                    "wrong_program" => withdrawal with { ProgramId = Uuid.CreateVersion5(ProgramId, "foreign") },
                    "wrong_subject" => withdrawal with { SubjectId = UnmatchedSupportId },
                    "wrong_tenant" => withdrawal with { TenantId = Uuid.CreateVersion5(TenantId, "foreign") },
                    "wrong_revision" => withdrawal with { Revision = 3 },
                    "wrong_expected_revision" => withdrawal with { OriginalExpectedRevision = 0 },
                    "empty_command" => withdrawal with { CommandId = Uuid.Empty },
                    "empty_actor" => withdrawal with { ActorMemberId = Uuid.Empty },
                    "empty_reason" => withdrawal with { Reason = "" },
                    "untrimmed_reason" => withdrawal with { Reason = " Mistaken link " },
                    "oversized_reason" => withdrawal with { Reason = new string('r', 2001) },
                    "oversized_actor_display" => withdrawal with { ActorDisplay = new string('a', 201) },
                    "invalid_digest" => withdrawal with { AuthoredIntentSha256 = new string('A', 64) },
                    "earlier_occurrence" => withdrawal with { OccurredAt = BeforeTypeIDate.AddMinutes(-1) },
                    "unknown_event" => new EvidenceArtifactInspected(TenantId, SupportId,
                        EvidenceArtifactStates.Available, null, withdrawal.OccurredAt),
                    _ => withdrawal with { },
                };
                var metadata = withdrawal.Metadata;
                changed.AttachMetadata(fault switch
                {
                    "wrong_metadata_version" => metadata with { AggregateVersion = 3 },
                    "wrong_metadata_time" => metadata with { OccurredOn = metadata.OccurredOn.AddMinutes(1) },
                    "wrong_metadata_identity" => metadata with { AggregateId = UnmatchedSupportId },
                    "duplicate_metadata_event" => metadata with
                    {
                        EventId = Uuid.CreateVersion5(SupportId, "evidence_support_linked_v1"),
                    },
                    "audit_metadata" => metadata with { IsAudit = true },
                    "earlier_occurrence" => metadata with { OccurredOn = BeforeTypeIDate.AddMinutes(-1) },
                    _ => metadata,
                });
                yield return record with { Event = changed };
                if (fault == "duplicate_withdrawal")
                    yield return record with { Event = changed };
            }
        }
    }

    sealed class AggregateReader(params Aggregate[] aggregates) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregates.FirstOrDefault(item => item.Id == aggregate.Id &&
                item.GetType() == typeof(TAggregate)) is TAggregate found ? found : aggregate);
    }
}
