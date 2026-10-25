using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class AccessExpectationsTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid SystemInstanceId = Uuid.CreateVersion4();
    static readonly Uuid ExpectationId = Uuid.CreateVersion4();
    static readonly Uuid SuccessorExpectationId = Uuid.CreateVersion4();
    static readonly Uuid ExceptionId = Uuid.CreateVersion4();
    static readonly Uuid PopulationExceptionId = Uuid.CreateVersion4();
    static readonly Uuid FutureExceptionId = Uuid.CreateVersion4();
    static readonly Uuid FuturePopulationExceptionId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateOnly TypeIDate = new(2027, 3, 31);
    static readonly DateTimeOffset TypeIThrough = new(2027, 3, 31, 23, 59, 59,
        TimeSpan.Zero);
    static readonly DateTimeOffset AfterTypeIDate = new(2027, 4, 1, 12, 0, 0,
        TimeSpan.Zero);
    static readonly ActorReference Proposer = ActorReference.ForMember(ProposerId, "Owner");
    static readonly ActorReference Approver = ActorReference.ForMember(ApproverId, "Approver");
    static readonly EventStreamAddress LineStream = new(TenantId.ToString(), AccessExpectation.Area,
        ExpectationId.ToString());

    [Fact]
    public async Task ShouldKeepAccessExpectationsStableGivenSuccessorAndExceptionsAfterTypeIDate()
    {
        // Arrange
        var before = await Fixture(includeApproval: true);
        var after = await Fixture(includeApproval: true, laterChanges: true);

        // Act
        var original = await before.Contributor.ReadAsync(Engagement(), before.Context,
            CancellationToken.None);
        var later = await after.Contributor.ReadAsync(Engagement(), after.Context,
            CancellationToken.None);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal("access_expectations", original.Value.Family);
        var source = Assert.Single(original.Value.Sources);
        var laterSource = Assert.Single(later.Value.Sources);
        Assert.Equal("access_expectation_ledger", source.Kind);
        Assert.Equal(SystemInstanceId, source.RecordId);
        Assert.Equal(4, source.Revision);
        Assert.Equal(source.VersionId, laterSource.VersionId);
        Assert.Equal(source.ContentSha256, laterSource.ContentSha256);
        Assert.Empty(original.Value.Blockers);
        Assert.Empty(later.Value.Blockers);
    }

    [Fact]
    public async Task ShouldKeepExpectationProposedGivenApprovalAfterTypeIDate()
    {
        // Arrange
        var proposed = await Fixture(includeApproval: false);
        var approvedLater = await Fixture(includeApproval: false, approvedAfterTypeIDate: true);

        // Act
        var before = await proposed.Contributor.ReadAsync(Engagement(), proposed.Context,
            CancellationToken.None);
        var after = await approvedLater.Contributor.ReadAsync(Engagement(), approvedLater.Context,
            CancellationToken.None);

        // Assert
        Assert.True(before.IsSuccess, before.Error?.Message);
        Assert.True(after.IsSuccess, after.Error?.Message);
        var original = Assert.Single(before.Value.Sources);
        var later = Assert.Single(after.Value.Sources);
        Assert.Equal(1, original.Revision);
        Assert.Equal(1, later.Revision);
        Assert.Equal(original.VersionId, later.VersionId);
        Assert.Equal(original.ContentSha256, later.ContentSha256);
    }

    [Fact]
    public async Task ShouldExcludeBackdatedExpectationGivenRecordedAfterTypeIDate()
    {
        // Arrange
        var fixture = await Fixture(includeApproval: false, recordedAt: AfterTypeIDate);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.ScopeDifferences, difference =>
            difference.Contains("no access expectation ledger recorded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldReadIndependentLinesAndExceptionsGivenOneSystemWithSeveralOwners()
    {
        // Arrange
        var fixture = await Fixture(includeApproval: true, secondLine: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(6, Assert.Single(result.Value.Sources).Revision);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldExcludeBackdatedExpectationGivenAppendCommitsAfterFrozenReceiptCut()
    {
        // Arrange
        var fixture = await Fixture(includeApproval: false, committedAt: AfterTypeIDate);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
    }

    [Fact]
    public async Task ShouldAcceptValidExpectationHistoryGivenOccurrenceMetadataDescendsBeforeReceiptCut()
    {
        // Arrange
        var fixture = await Fixture(includeApproval: true, committedAt: TypeIThrough.AddSeconds(-1), descendingOccurrence: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(4, Assert.Single(result.Value.Sources).Revision);
    }

    [Fact]
    public async Task ShouldAcceptIndependentExceptionsGivenBusinessClockRegressesWithinValidReceiptPrefix()
    {
        // Arrange
        var fixture = await Fixture(includeApproval: true, committedAt: TypeIThrough.AddSeconds(-1), descendingBodyTimes: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(5, Assert.Single(result.Value.Sources).Revision);
        Assert.Empty(result.Value.Blockers);
    }

    static AccessExpectationView View(Uuid expectationId, long revision, DateTimeOffset proposedAt,
        string ruleKind, AccessExpectationParameters parameters, Uuid? supersedes = null) => new(expectationId,
        SystemInstanceId, supersedes, ruleKind, parameters, "Human access must use federated roles.",
        new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), null, AccessExpectation.Proposed, Proposer,
        proposedAt, null, null, null, revision);

    static async Task<FixtureState> Fixture(bool includeApproval, bool laterChanges = false,
        DateTimeOffset? recordedAt = null, bool approvedAfterTypeIDate = false, bool secondLine = false,
        DateTimeOffset? committedAt = null, bool descendingOccurrence = false, bool descendingBodyTimes = false)
    {
        var content = AccessReviewFacts.Content();
        var events = new InMemoryEventStore();
        var lines = new List<AccessExpectation>();
        var facts = new List<DomainEvent>();
        ulong version = 0;
        long revision = 0;

        async Task<ImmutableContentRef> Write<T>(string schema, T value) where T : notnull =>
            await content.WriteJsonAsync(TenantId.ToString(), Uuid.CreateVersion4(), schema, value);

        async Task Propose(EventStreamAddress stream, Uuid lineId, Uuid expectationId, DateTimeOffset proposedAt,
            string ruleKind, AccessExpectationParameters parameters, Uuid? supersedes = null,
            DateTimeOffset? occurredAt = null)
        {
            revision++;
            version++;
            var view = View(expectationId, revision, proposedAt, ruleKind, parameters, supersedes);
            facts.Add(AccessReviewFacts.Attach(new AccessExpectationProposed(TenantId, SystemInstanceId, revision,
                ProposerId, await Write("AccessExpectationProposed.ExpectationContent.v1", view),
                AccessReviewFacts.Provenance(stream, "ProposeAccessExpectation", expectationId, Proposer, 0,
                    proposedAt), AccessReviewFacts.Response(), AccessReviewFacts.Response()), lineId, version,
                occurredAt ?? proposedAt));
        }

        void Approve(EventStreamAddress stream, Uuid lineId, Uuid expectationId, DateTimeOffset approvedAt,
            DateTimeOffset? occurredAt = null)
        {
            revision++;
            version++;
            facts.Add(AccessReviewFacts.Attach(new AccessExpectationApproved(TenantId, SystemInstanceId, revision,
                expectationId, Approver, approvedAt, null,
                AccessReviewFacts.Provenance(stream, "ApproveAccessExpectation", Uuid.CreateVersion4(), Approver,
                    (ulong)revision - 1, approvedAt), AccessReviewFacts.Response(), AccessReviewFacts.Response()),
                lineId, version, occurredAt ?? approvedAt));
        }

        async Task Except(EventStreamAddress stream, Uuid lineId, Uuid exceptionId, Uuid expectationId,
            string subject, DateTimeOffset expiresAt, DateTimeOffset approvedAt, DateTimeOffset? occurredAt = null)
        {
            revision++;
            version++;
            var view = new AccessExpectationExceptionView(exceptionId, expectationId, subject, null,
                "Temporary migration exception", expiresAt, Approver, approvedAt);
            facts.Add(AccessReviewFacts.Attach(new AccessExpectationExceptionRecorded(TenantId, SystemInstanceId,
                revision, await Write("AccessExpectationExceptionRecorded.ExceptionContent.v1", view),
                AccessReviewFacts.Provenance(stream, "ExemptAccessExpectation", exceptionId, Approver,
                    (ulong)revision - 1, approvedAt), AccessReviewFacts.Response(), AccessReviewFacts.Response()),
                lineId, version, occurredAt ?? approvedAt));
        }

        var forbidden = new AccessExpectationParameters(PrincipalKind: AccessReviewVocabulary.UserAccount);
        await Propose(LineStream, ExpectationId, ExpectationId, new DateTimeOffset(2027, 1, 1, 12, 0, 0,
            TimeSpan.Zero), AccessReviewVocabulary.ForbiddenPrincipalKind, forbidden,
            occurredAt: recordedAt);
        if (includeApproval)
        {
            Approve(LineStream, ExpectationId, ExpectationId, new DateTimeOffset(2027, 2, 1, 12, 0, 0,
                TimeSpan.Zero), recordedAt);
            await Except(LineStream, ExpectationId, ExceptionId, ExpectationId, "legacy-user-1",
                new DateTimeOffset(2027, 4, 30, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero), recordedAt);
        }

        if (descendingBodyTimes)
            await Except(LineStream, ExpectationId, FutureExceptionId, ExpectationId, "independent-legacy-user",
                new DateTimeOffset(2027, 4, 30, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 2, 15, 12, 0, 0, TimeSpan.Zero));

        if (approvedAfterTypeIDate)
            Approve(LineStream, ExpectationId, ExpectationId, AfterTypeIDate);
        if (laterChanges)
        {
            await Propose(LineStream, ExpectationId, SuccessorExpectationId, AfterTypeIDate,
                AccessReviewVocabulary.PrivilegedEntitlement,
                new AccessExpectationParameters(EntitlementKind: "admin_privilege"), ExpectationId);
            Approve(LineStream, ExpectationId, SuccessorExpectationId, AfterTypeIDate.AddMinutes(1));
            await Except(LineStream, ExpectationId, FutureExceptionId, ExpectationId, "legacy-user-2",
                new DateTimeOffset(2027, 8, 1, 0, 0, 0, TimeSpan.Zero), AfterTypeIDate.AddMinutes(2));
        }

        if (descendingOccurrence && facts[1] is AccessExpectationApproved approval)
        {
            var descending = approval with { };
            descending.AttachMetadata(approval.Metadata with { OccurredOn = facts[0].Metadata.OccurredOn.AddDays(-1) });
            facts[1] = descending;
        }
        var attached = facts.ToArray();
        await events.AppendAsync(LineStream, 0, attached);
        lines.Add(new AggregateScenario<AccessExpectation>(new AccessExpectation(TenantId, ExpectationId))
            .Given(attached).Aggregate);

        if (secondLine)
        {
            var lineId = Uuid.CreateVersion4();
            var stream = new EventStreamAddress(TenantId.ToString(), AccessExpectation.Area, lineId.ToString());
            facts.Clear();
            version = 0;
            revision = 0;
            await Propose(stream, lineId, lineId, new DateTimeOffset(2027, 1, 2, 12, 0, 0, TimeSpan.Zero),
                AccessReviewVocabulary.PrivilegedEntitlement,
                new AccessExpectationParameters(EntitlementKind: "admin_privilege"));
            Approve(stream, lineId, lineId, new DateTimeOffset(2027, 2, 2, 12, 0, 0, TimeSpan.Zero));
            var second = facts.ToArray();
            await events.AppendAsync(stream, 0, second);
            lines.Add(new AggregateScenario<AccessExpectation>(new AccessExpectation(TenantId, lineId))
                .Given(second).Aggregate);
        }

        if (includeApproval)
            await AccessReviewFacts.AppendPopulationExceptionAsync(events, content, TenantId, SystemInstanceId,
                PopulationExceptionId, "The accepted population is pending owner attestation.",
                new DateTimeOffset(2027, 4, 15, 0, 0, 0, TimeSpan.Zero), Approver,
                new DateTimeOffset(2027, 3, 2, 12, 0, 0, TimeSpan.Zero), recordedAt);
        if (laterChanges)
            await AccessReviewFacts.AppendPopulationExceptionAsync(events, content, TenantId, SystemInstanceId,
                FuturePopulationExceptionId, "Later export exception.",
                new DateTimeOffset(2027, 8, 1, 0, 0, 0, TimeSpan.Zero), Approver, AfterTypeIDate.AddMinutes(3));

        var contributor = new AccessExpectationsTypeIBaselineSourceContributor(new LineReader(lines), events,
            content);
        var context = Context();
        if (committedAt is { } at)
        {
            var receipts = new ScriptedNativeEventReceipts();
            await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()), EventCursor.Start, CancellationToken.None))
                receipts.Record(record, at);
            context = context with
            {
                RecordedThrough = TypeIThrough,
                ReceiptCut = new RecordedSourceReceiptCutFactory(events, receipts).Create(TenantId, TypeIThrough)
            };
        }
        return new FixtureState(contributor, context);
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", TypeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1, TypeIThrough, ApproverId, "Examiner");

    static TypeIBaselineSourceReadContext Context() => new(Uuid.CreateVersion4(),
    [
        new TypeIBaselineSourceReference("reviewed_system_revision", SystemInstanceId,
            Uuid.CreateVersion5(SystemInstanceId, "reviewed_system_revision_v1:1"), 1,
            new string('a', 64)),
    ]);

    sealed record FixtureState(ITypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class LineReader(IReadOnlyList<AccessExpectation> lines) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is AccessExpectation requested &&
                                 lines.FirstOrDefault(line => line.Id == requested.Id) is { } line
                ? (TAggregate)(Aggregate)line
                : aggregate);
    }
}
