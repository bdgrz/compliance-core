using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class CommitmentTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid ServiceId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset DayOne = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly VersionStart = new(2026, 5, 1);

    [Fact]
    public async Task ShouldCaptureEffectiveApprovedCommitmentVersionGivenLaterSuccessor()
    {
        // Arrange
        var fixture = await Fixture("user_entity_responsibility", includeSuccessor: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("commitments", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("commitment_version", source.Kind);
        Assert.Equal(fixture.DraftId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(fixture.DraftId, "commitment_version_v1:1"),
            source.VersionId);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldCaptureScheduledRetirementGivenRetirementRecordedByTypeIDate()
    {
        // Arrange
        var fixture = await Fixture("service_commitment", retire: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(["commitment_version", "commitment_retirement"],
            result.Value.Sources.Select(static source => source.Kind));
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("scheduled after the Type I date", StringComparison.Ordinal));
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldBlockCommitmentGivenNoApprovedVersionEffectiveOnTypeIDate()
    {
        // Arrange
        var fixture = await Fixture("service_commitment", approve: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("approved version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldRejectCommitmentHistoryGivenReviewAfterRetirement()
    {
        // Arrange
        var fixture = await Fixture("service_commitment", retire: true,
            reviewAfterRetirement: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectApprovalGivenAcceptedReviewFromSupersededRevision()
    {
        // Arrange
        var fixture = await Fixture("service_commitment", approve: false,
            staleApprovalAfterRevision: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldLeaveSystemRequirementToItsOwnFamilyGivenCommitmentFamilyRead()
    {
        // Arrange
        var fixture = await Fixture("system_requirement", includeService: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldCaptureSystemRequirementVersionGivenItsOwnFamily()
    {
        // Arrange
        var fixture = await Fixture("system_requirement", family: "system_requirements");

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("system_requirements", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("system_requirement_version", source.Kind);
        Assert.Equal(Uuid.CreateVersion5(fixture.DraftId,
            "system_requirement_version_v1:1"), source.VersionId);
        Assert.Empty(result.Value.Blockers);
    }

    static async Task<FixtureState> Fixture(string kind, bool includeSuccessor = false,
        bool retire = false, bool approve = true, bool reviewAfterRetirement = false,
        bool includeService = true, bool staleApprovalAfterRevision = false,
        string family = "commitments")
    {
        var draftId = CommitmentDraft.IdFor(TenantId, ProgramId, kind, "SC-01");
        var draft = new CommitmentDraft(TenantId, draftId);
        Assert.True(draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId, kind, "SC-01",
            "Protect customer data", "Payroll service", "MSA v3, section 4.1", AuthorId,
            "Author", DayOne).IsSuccess);
        Uuid? staleReviewId = null;
        if (staleApprovalAfterRevision)
        {
            staleReviewId = Uuid.CreateVersion4();
            Assert.Null(draft.Review(ProgramId, 1, staleReviewId.Value, "accept",
                "Security lead", "applicable", "supported", "Verified against the source",
                "Evidence reviewed", ReviewerId, "Reviewer", DayOne.AddDays(1),
                sourceVerifiedReference: "MSA v3, section 4.1",
                sourceEvidence: "Signed MSA v3, section 4.1"));
            Assert.Null(draft.Revise(ProgramId, 1, "Protect customer data at rest",
                "Payroll service", "MSA v3, section 4.2", AuthorId, "Author",
                DayOne.AddDays(3)));
        }
        else if (approve)
            Approve(draft, 1, VersionStart, "MSA v3, section 4.1");
        if (includeSuccessor)
        {
            Assert.Null(draft.Revise(ProgramId, 1, "Protect customer data at rest",
                "Payroll service", "MSA v3, section 4.2", AuthorId, "Author",
                DayOne.AddDays(5)));
            Approve(draft, 2, new DateOnly(2026, 7, 1), "MSA v3, section 4.2");
        }
        if (retire)
            Assert.Null(draft.Retire(ProgramId, draft.Revision, new DateOnly(2026, 7, 15),
                "The commitment was superseded.", ApproverId, "Approver", DayOne.AddDays(10)));

        var pending = new AggregateScenario<CommitmentDraft>(draft).PendingEvents;
        var additionalEvents = new List<DomainEvent>();
        if (reviewAfterRetirement)
            additionalEvents.Add(new CommitmentReviewed(TenantId, ProgramId, draftId,
                draft.Revision,
                    Uuid.CreateVersion4(), "request_changes", null, null, null, null,
                    "Late review after retirement", null, null, null, ReviewerId,
                    "Reviewer", DayOne.AddDays(12)));
        if (staleApprovalAfterRevision)
            additionalEvents.Add(new CommitmentApproved(TenantId, ProgramId, draftId, 1,
                Uuid.CreateVersion4(), staleReviewId!.Value, 1, VersionStart, "impact-digest",
                "Approval after superseding revision", ApproverId, "Approver",
                DayOne.AddDays(5))
            {
                StoredActor = Bdgrz.Compliance.Features.AccessControl.ActorReference.ForMember(
                    ApproverId, "Approver"),
            });
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(TenantId.ToString(), "commitment-drafts",
            draftId.ToString());
        var attached = pending.ToList();
        var firstAdditionalPosition = (ulong)attached.Count;
        attached.AddRange(additionalEvents.Select((domainEvent, index) => DomainEventSeed.Attach(
            domainEvent, draftId, firstAdditionalPosition + (ulong)index + 1)));
        await events.AppendAsync(stream, 0, attached);
        var checkpoint = await CheckpointAfterAsync(events);
        var source = new AggregateScenario<CommitmentDraft>(new CommitmentDraft(TenantId, draftId))
            .Given(attached.ToArray()).Aggregate;
        var directory = new CommitmentDirectory(draftId, Versions(attached), checkpoint);
        var aggregateReader = new CommitmentAggregateReader(source);
        var versionConsistency = new CommitmentVersionReadConsistency(directory, aggregateReader);
        var commitmentConsistency = new CommitmentDraftListReadConsistency(directory, events);
        var entries = new List<BoundaryScopeEntry>();
        if (includeService)
            entries.Add(new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "service",
                ServiceId.ToString(), ServiceId, "Service owner", "The service is in scope", false));
        entries.Add(new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "commitment",
            draftId.ToString(), draftId, "Compliance lead", "The commitment is in scope", false));
        var version = new BoundaryVersionView(TenantId, BoundaryId, ProgramId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("In-scope commitments", "type_i",
                ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
                entries), "approved", new DateOnly(2026, 1, 1), AuthorId, "Examiner", DayOne);
        var boundary = new BoundaryView(TenantId, BoundaryId, ProgramId, null, version, null, 1);
        var boundaries = new BoundaryDirectory(boundary, version, checkpoint);
        var boundaryConsistency = new ProgramSetupWorkReadConsistency(
            new ProgramDirectory(checkpoint), boundaries, events);
        var sourceReader = new CommitmentTypeIBaselineSourceReader(aggregateReader, events,
            versionConsistency, commitmentConsistency, boundaries, boundaryConsistency);
        ITypeIBaselineSourceContributor contributor = family switch
        {
            "commitments" => new CommitmentTypeIBaselineSourceContributor(sourceReader),
            "system_requirements" => new SystemRequirementTypeIBaselineSourceContributor(sourceReader),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown source family."),
        };
        var boundarySource = new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
            version.VersionId, version.Revision,
            SnapshotContentIdentity.ApprovedBoundaryVersion(version));
        return new FixtureState(draftId, contributor,
            new TypeIBaselineSourceReadContext(Uuid.CreateVersion4(), [boundarySource]));
    }

    static void Approve(CommitmentDraft draft, long revision, DateOnly effectiveFrom,
        string sourceReference)
    {
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(draft.Review(ProgramId, revision, reviewId, "accept", "Security lead",
            "applicable", "supported", "Verified against the source", "Evidence reviewed",
            ReviewerId, "Reviewer", DayOne.AddDays(revision), sourceVerifiedReference: sourceReference,
            sourceEvidence: "Signed MSA v3, section 4.1"));
        Assert.Null(draft.Approve(ProgramId, revision, Uuid.CreateVersion4(), reviewId,
            effectiveFrom, "Approved for the engagement", "impact-digest", ApproverId,
            "Approver", DayOne.AddDays(revision + 1)));
    }

    static CommitmentVersionView[] Versions(List<DomainEvent> events)
    {
        var created = Assert.IsType<CommitmentDraftCreated>(events[0]);
        var revisions = events.OfType<CommitmentDraftRevised>().ToDictionary(
            static revision => revision.Revision);
        return events.OfType<CommitmentApproved>().Select(approved =>
        {
            var reviewed = Assert.Single(events.OfType<CommitmentReviewed>(), review =>
                review.DecisionId == approved.AcceptedReviewDecisionId);
            var statement = approved.Revision == 1 ? created.Statement : revisions[approved.Revision].Statement;
            var context = approved.Revision == 1 ? created.Context : revisions[approved.Revision].Context;
            var sourceReference = approved.Revision == 1
                ? created.SourceReference
                : revisions[approved.Revision].SourceReference;
            var decision = new CommitmentDecisionView(TenantId, ProgramId, created.DraftId,
                reviewed.DecisionId, reviewed.Revision, reviewed.Outcome, reviewed.OwnerReference,
                reviewed.Applicability, reviewed.Interpretation, reviewed.InterpretationNote,
                reviewed.Rationale, reviewed.Version, reviewed.EffectiveFrom, reviewed.ImpactDigest,
                reviewed.ActorMemberId, reviewed.ActorDisplay, reviewed.DecidedAt,
                reviewed.SeparationOfDutiesWaiverId, "review", null,
                reviewed.SourceVerification, reviewed.SourceVerifiedReference, reviewed.SourceEvidence)
            {
                Actor = reviewed.Actor,
            };
            var approval = new CommitmentDecisionView(TenantId, ProgramId, created.DraftId,
                approved.DecisionId, approved.Revision, "approve", null, null, null, null,
                approved.Rationale, approved.Version, approved.EffectiveFrom,
                approved.ImpactDigest, approved.ActorMemberId, approved.ActorDisplay,
                approved.DecidedAt, approved.SeparationOfDutiesWaiverId, "approval",
                approved.AcceptedReviewDecisionId)
            {
                Actor = approved.Actor,
            };
            return new CommitmentVersionView(TenantId, ProgramId, created.DraftId,
                created.ServiceId, created.Kind, created.Identifier, approved.Version,
                approved.Revision, statement, context, sourceReference,
                reviewed.OwnerReference!, reviewed.Applicability!, reviewed.Interpretation!,
                reviewed.InterpretationNote, CommitmentDraft.PerformedBy(created.Kind),
                CommitmentDraft.IsInternallyPerformed(created.Kind), approved.EffectiveFrom,
                decision, "verified", reviewed.SourceEvidence, approval)
            {
                ProviderId = created.ProviderId,
            };
        }).ToArray();
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", new DateOnly(2026, 6, 30), null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        BoundaryId, [], "draft", 1, DayOne, AuthorId, "Examiner");

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(
                           EventStreamPattern.ForPattern(TenantId.ToString(), "commitment-drafts"),
                           cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed record FixtureState(Uuid DraftId,
        ITypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class CommitmentAggregateReader(CommitmentDraft draft) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is CommitmentDraft
                ? (TAggregate)(Aggregate)draft
                : aggregate);
    }

    sealed class CommitmentDirectory(Uuid draftId,
        IReadOnlyList<CommitmentVersionView> versions, ProjectionCheckpoint checkpoint)
        : ICommitmentDraftDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
        public ValueTask<CommitmentDraftView?> GetAsync(Uuid tenantId, Uuid id,
            CancellationToken ct = default) => ValueTask.FromResult<CommitmentDraftView?>(null);
        public ValueTask<Page<CommitmentDraftView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<CommitmentDraftView>([], null));
        public ValueTask<CommitmentDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid id, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<CommitmentDraftRevisionView?>(null);
        public ValueTask<CommitmentVersionView?> GetVersionAsync(Uuid tenantId,
            Uuid id, long version, CancellationToken ct = default) =>
            ValueTask.FromResult(id == draftId ? versions.SingleOrDefault(item =>
                item.Version == version) : null);
        public ValueTask<Page<CommitmentVersionView>> ListVersionsAsync(Uuid tenantId,
            Uuid id, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<CommitmentVersionView>([], null));
        public ValueTask<Page<CommitmentDecisionView>> ListDecisionsAsync(Uuid tenantId,
            Uuid id, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<CommitmentDecisionView>([], null));
        public ValueTask<CommitmentDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<CommitmentDecisionView?>(null);
    }

    sealed class BoundaryDirectory(BoundaryView boundary, BoundaryVersionView version,
        ProjectionCheckpoint checkpoint) : IBoundaryDirectoryReader
    {
        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(boundary);
        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<BoundaryView>([boundary], null));
        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) => ValueTask.FromResult<BoundaryVersionView?>(version);
        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryVersionView>?>(new Page<BoundaryVersionView>([version], null));
        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(version);
        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryDecisionView?>(null);
        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryDecisionView>?>(new Page<BoundaryDecisionView>([], null));
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
    }

    sealed class ProgramDirectory(ProjectionCheckpoint checkpoint) : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(null);
        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProgramView>([], null));
        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(new Page<ProgramRevisionView>([], null));
        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) => ValueTask.FromResult<ProgramRevisionView?>(null);
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
    }
}
