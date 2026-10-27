using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

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

    static async Task<FixtureState> Fixture(bool includeLaterLink, bool includeHold = false,
        bool includeLaterReport = false, bool includeDisclosure = false,
        bool includeLaterDisclosure = false, bool includePriorHandlingPolicy = false,
        bool includeFutureHandlingPolicy = false, bool lateArtifactReceipt = false)
    {
        var store = new InMemoryEventStore();
        var matching = Support(SupportId, ControlId, 2, BeforeTypeIDate);
        var unmatched = Support(UnmatchedSupportId, Uuid.CreateVersion4(), 2,
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

        ITypeIBaselineSourceContributor contributor =
            new EvidenceGovernanceTypeIBaselineSourceContributor(store,
                new AggregateReader(fixtureAggregates.ToArray()));
        var effectiveControlVersion = new TypeIBaselineSourceReference("control_version",
            ControlId, Uuid.CreateVersion4(), 2, new string('b', 64));
        var context = new TypeIBaselineSourceReadContext(MemberId, [effectiveControlVersion]);
        if (lateArtifactReceipt)
        {
            var cutoff = AfterTypeIDate.AddDays(3);
            var receipts = new ScriptedNativeEventReceipts();
            await foreach (var record in store.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()), EventCursor.Start, CancellationToken.None))
                receipts.Record(record, record.Event is EvidenceArtifactDownloadIssued ? cutoff.AddMilliseconds(1) : cutoff.AddMilliseconds(-1));
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

    sealed class AggregateReader(params Aggregate[] aggregates) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregates.FirstOrDefault(item => item.Id == aggregate.Id &&
                item.GetType() == typeof(TAggregate)) is TAggregate found ? found : aggregate);
    }
}
