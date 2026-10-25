using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
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
    static readonly Uuid ProviderId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset DayOne = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly VersionStart = new(2026, 5, 1);

    [Fact]
    public async Task ShouldCaptureEffectiveApprovedCommitmentVersionGivenLaterSuccessor()
    {
        // Arrange
        await using var fixture = await Fixture("user_entity_responsibility", includeSuccessor: true);

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
        await using var fixture = await Fixture("service_commitment", retire: true);

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
        await using var fixture = await Fixture("service_commitment", approve: false);

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
        await using var fixture = await Fixture("service_commitment", retire: true,
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
        await using var fixture = await Fixture("service_commitment", approve: false,
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
        await using var fixture = await Fixture("system_requirement", includeService: false);

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
        await using var fixture = await Fixture("system_requirement", family: "system_requirements");

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

    [Fact]
    public async Task ShouldResolveOnlyEffectiveApplicableCsocsForTheirProviderGivenCommitmentCapture()
    {
        // Arrange
        await using var fixture = await Fixture("subservice_responsibility", providerId: ProviderId);
        var captured = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);
        Assert.True(captured.IsSuccess, captured.Error?.Message);
        Assert.DoesNotContain(captured.Value.Sources, static source =>
            source.Kind == "provider_csocs");
        var context = fixture.Context with
        {
            PreviouslyCapturedSources = Array.AsReadOnly(
                fixture.Context.PreviouslyCapturedSources.Concat(captured.Value.Sources).ToArray()),
        };

        // Act
        var result = await fixture.SourceReader.ReadEffectiveProviderCsocsAsync(Engagement(),
            context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([ProviderId], result.Value);
    }

    [Fact]
    public async Task ShouldExcludeCsocGivenProviderCommitmentRetiredByTypeIDate()
    {
        // Arrange
        await using var fixture = await Fixture("subservice_responsibility", retire: true,
            providerId: ProviderId, retirementEffectiveOn: new DateOnly(2026, 6, 1));
        var captured = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);
        Assert.True(captured.IsSuccess, captured.Error?.Message);
        var context = fixture.Context with
        {
            PreviouslyCapturedSources = Array.AsReadOnly(
                fixture.Context.PreviouslyCapturedSources.Concat(captured.Value.Sources).ToArray()),
        };

        // Act
        var result = await fixture.SourceReader.ReadEffectiveProviderCsocsAsync(Engagement(),
            context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyNativeContentFailureGivenUnavailableOrCorruptChange(bool unavailable)
    {
        // Arrange
        await using var fixture = await Fixture("service_commitment", unavailableContent: unavailable);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectEffectiveApprovalGivenApprovalAfterFrozenReceiptCutoff()
    {
        // Arrange
        await using var fixture = await Fixture("service_commitment");
        var context = await ReceiptContextAsync(fixture, DayOne.AddDays(1));

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectEffectiveRetirementGivenRetirementAfterFrozenReceiptCutoff()
    {
        // Arrange
        await using var fixture = await Fixture("service_commitment", retire: true,
            retirementEffectiveOn: new DateOnly(2026, 6, 1));
        var context = await ReceiptContextAsync(fixture, DayOne.AddDays(9));

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    static async Task<TypeIBaselineSourceReadContext> ReceiptContextAsync(FixtureState fixture, DateTimeOffset through)
    {
        var receipts = new ScriptedNativeEventReceipts();
        var stream = new EventStreamAddress(TenantId.ToString(), CommitmentDraft.Area, fixture.DraftId.ToString());
        await foreach (var record in fixture.Source.Events.ReadAsync(stream, 0, CancellationToken.None))
            receipts.Record(record, record.Event is CommitmentRetired ? DayOne.AddDays(10) : DayOne.AddDays(2));
        return fixture.Context with
        {
            RecordedThrough = through,
            ReceiptCut = new RecordedSourceReceiptCutFactory(fixture.Source.Events, receipts).Create(TenantId, through)
        };
    }

    static async Task<FixtureState> Fixture(string kind, bool includeSuccessor = false,
        bool retire = false, bool approve = true, bool reviewAfterRetirement = false,
        bool includeService = true, bool staleApprovalAfterRevision = false,
        string family = "commitments", Uuid? providerId = null,
        DateOnly? retirementEffectiveOn = null, bool? unavailableContent = null)
    {
        var draftId = CommitmentDraft.IdFor(TenantId, ProgramId, kind, "SC-01");
        var draft = new CommitmentRules(TenantId, draftId);
        Assert.True(draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId, kind, "SC-01",
            "Protect customer data", "Payroll service", "MSA v3, section 4.1", AuthorId,
            "Author", DayOne, providerId).IsSuccess);
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
        // Keep the authoritative native source valid. The malformed-history cases below
        // replace only the history dependency at the same exact native source cut.
        if (reviewAfterRetirement)
            Assert.Null(draft.Revise(ProgramId, draft.Revision, "A later working draft",
                "Payroll service", "MSA v3, section 4.2", AuthorId, "Author", DayOne.AddDays(8)));
        if (staleApprovalAfterRevision)
            Approve(draft, 2, VersionStart, "MSA v3, section 4.2");
        if (retire)
            Assert.Null(draft.Retire(ProgramId, draft.Revision,
                retirementEffectiveOn ?? new DateOnly(2026, 7, 15),
                "The commitment was superseded.", ApproverId, "Approver", DayOne.AddDays(10)));

        var source = new CommitmentSourceFixture();
        await source.SeedAsync(draft);
        var facts = await source.FactsAsync(TenantId, draftId);
        var events = source.Events;
        if (reviewAfterRetirement || staleApprovalAfterRevision)
        {
            // Explicit immutable facts are deliberately not appended through the native
            // owner. The baseline must reject a corrupt history dependency independently.
            var history = facts.Take(3).ToList();
            var historical = new CommitmentRules(TenantId, draftId);
            foreach (var fact in history.Cast<ICommitmentFact>())
                foreach (var change in (await CommitmentFactContent.ReadChangeAsync(source.Content,
                             fact, CancellationToken.None)).Changes)
                    historical.Apply(change);
            if (reviewAfterRetirement)
            {
                await AddHistoryAsync(new CommitmentRetiredContent(TenantId, ProgramId, draftId,
                    1, ApproverId, "Approver", new DateOnly(2026, 7, 15),
                    "The commitment was superseded.", DayOne.AddDays(10)),
                    nameof(RetireCommitment), ApproverId, "Approver", DayOne.AddDays(10), Uuid.CreateVersion4());
                var decision = Uuid.CreateVersion4();
                await AddHistoryAsync(new CommitmentReviewedContent(TenantId, ProgramId, draftId,
                    1, decision, "request_changes", null, null, null, null,
                    "Late review after retirement", null, null, null, ReviewerId, "Reviewer",
                    DayOne.AddDays(12)), nameof(ReviewCommitmentDraft), ReviewerId, "Reviewer",
                    DayOne.AddDays(12), decision);
            }
            else
            {
                var decision = Uuid.CreateVersion4();
                await AddHistoryAsync(new CommitmentReviewedContent(TenantId, ProgramId, draftId,
                    2, decision, "request_changes", null, null, null, null,
                    "The new revision needs changes", null, null, null, ReviewerId, "Reviewer",
                    DayOne.AddDays(4)), nameof(ReviewCommitmentDraft), ReviewerId, "Reviewer",
                    DayOne.AddDays(4), decision);
                var approval = Uuid.CreateVersion4();
                await AddHistoryAsync(new CommitmentApprovedContent(TenantId, ProgramId, draftId,
                    1, approval, staleReviewId!.Value, 1, VersionStart, "impact-digest",
                    "Approval after superseding revision", ApproverId, "Approver", DayOne.AddDays(5)),
                    nameof(ApproveCommitmentDraft), ApproverId, "Approver", DayOne.AddDays(5), approval);
            }
            events = new InMemoryEventStore();
            await events.AppendAsync(new EventStreamAddress(TenantId.ToString(), CommitmentDraft.Area,
                draftId.ToString()), 0, history);

            async Task AddHistoryAsync(ICommitmentMutationContent change, string kind, Uuid member,
                string display, DateTimeOffset at, Uuid command)
            {
                var gate = historical.Revision;
                historical.Apply(change);
                var operation = OwnerProvenance.For(new CommitmentDraft(TenantId, draftId), command,
                    kind, ActorReference.ForMember(member, display), draftId.ToString(),
                    unchecked((ulong)gate), AuthoredIntentDigest.Compute(change), at);
                var fact = await CommitmentSeeds.FreezeAsync(source.Content, historical, [change],
                    operation, history.Count + 1);
                history.Add(DomainEventSeed.Attach(fact, draftId, (ulong)history.Count + 1));
            }
        }
        var checkpoint = await CheckpointAfterAsync(events);
        var directory = new CommitmentDirectory(draftId, draft.ReadVersions(), checkpoint);
        var content = unavailableContent is { } unavailable
            ? new UnreadableChange(source.Content, unavailable) : source.Content;
        var commitments = new CommitmentReader(source.Reader, source.Events, content);
        var versionConsistency = new CommitmentVersionReadConsistency(directory, commitments);
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
        var sourceReader = new CommitmentTypeIBaselineSourceReader(commitments, events, content,
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
            new TypeIBaselineSourceReadContext(Uuid.CreateVersion4(), [boundarySource]),
            sourceReader, source);
    }

    static void Approve(CommitmentRules draft, long revision, DateOnly effectiveFrom,
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
        TypeIBaselineSourceReadContext Context,
        CommitmentTypeIBaselineSourceReader SourceReader,
        CommitmentSourceFixture Source) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }

    sealed class UnreadableChange(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema != CommitmentFactContent.ChangeSchema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            throw unavailable
                ? new ImmutableContentUnavailableException("The exact native change is not visible yet.")
                : new ImmutableContentIntegrityException("The exact native change digest differs.");
        }
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
            Uuid draftId, Uuid decisionId, CancellationToken ct = default) =>
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
