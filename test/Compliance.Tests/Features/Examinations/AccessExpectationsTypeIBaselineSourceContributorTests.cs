using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Examinations;
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

    [Fact]
    public async Task ShouldKeepAccessExpectationsStableGivenSuccessorAndExceptionsAfterTypeIDate()
    {
        // Arrange
        var before = await Fixture(CreateBaseLedger(includeApproval: true));
        var after = await Fixture(CreateLedgerWithLaterChanges());

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
        var proposed = await Fixture(CreateBaseLedger(includeApproval: false));
        var approvedLaterLedger = CreateBaseLedger(includeApproval: false);
        Assert.True(approvedLaterLedger.Approve(ExpectationId, approvedLaterLedger.Revision,
            ApproverId, Approver, AfterTypeIDate, null).IsSuccess);
        var approvedLater = await Fixture(approvedLaterLedger);

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
        var fixture = await Fixture(CreateBaseLedger(includeApproval: false), AfterTypeIDate);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.ScopeDifferences, difference =>
            difference.Contains("no access expectation ledger recorded", StringComparison.Ordinal));
    }

    static AccessReviewSystemLedger CreateBaseLedger(bool includeApproval)
    {
        var ledger = new AccessReviewSystemLedger(TenantId, SystemInstanceId);
        Assert.True(ledger.Propose(0, ExpectationId,
            AccessReviewVocabulary.ForbiddenPrincipalKind,
            new AccessExpectationParameters(PrincipalKind: AccessReviewVocabulary.UserAccount),
            "Human access must use federated roles.", new DateTimeOffset(2027, 1, 1, 0, 0, 0,
                TimeSpan.Zero), null, null, ProposerId, Proposer,
            new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero)).IsSuccess);
        if (!includeApproval)
            return ledger;

        Assert.True(ledger.Approve(ExpectationId, ledger.Revision, ApproverId, Approver,
            new DateTimeOffset(2027, 2, 1, 12, 0, 0, TimeSpan.Zero), null).IsSuccess);
        Assert.True(ledger.RecordException(ExceptionId, ExpectationId, ledger.Revision,
            "legacy-user-1", null, "Temporary migration exception",
            new DateTimeOffset(2027, 4, 30, 0, 0, 0, TimeSpan.Zero), Approver,
            new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero)).IsSuccess);
        Assert.True(ledger.RecordPopulationException(PopulationExceptionId, ledger.Revision,
            "The accepted population is pending owner attestation.",
            new DateTimeOffset(2027, 4, 15, 0, 0, 0, TimeSpan.Zero), Approver,
            new DateTimeOffset(2027, 3, 2, 12, 0, 0, TimeSpan.Zero)).IsSuccess);
        return ledger;
    }

    static AccessReviewSystemLedger CreateLedgerWithLaterChanges()
    {
        var ledger = CreateBaseLedger(includeApproval: true);
        Assert.True(ledger.Propose(ledger.Revision, SuccessorExpectationId,
            AccessReviewVocabulary.PrivilegedEntitlement,
            new AccessExpectationParameters(EntitlementKind: "admin_privilege"),
            "Privileged entitlements require named review.",
            new DateTimeOffset(2027, 4, 15, 0, 0, 0, TimeSpan.Zero), null, ExpectationId,
            ProposerId, Proposer, AfterTypeIDate).IsSuccess);
        Assert.True(ledger.Approve(SuccessorExpectationId, ledger.Revision, ApproverId,
            Approver, AfterTypeIDate.AddMinutes(1), null).IsSuccess);
        Assert.True(ledger.RecordException(FutureExceptionId, ExpectationId, ledger.Revision,
            "legacy-user-2", null, "Later temporary exception",
            new DateTimeOffset(2027, 8, 1, 0, 0, 0, TimeSpan.Zero), Approver,
            AfterTypeIDate.AddMinutes(2)).IsSuccess);
        Assert.True(ledger.RecordPopulationException(FuturePopulationExceptionId,
            ledger.Revision, "Later export exception.",
            new DateTimeOffset(2027, 8, 1, 0, 0, 0, TimeSpan.Zero), Approver,
            AfterTypeIDate.AddMinutes(3)).IsSuccess);
        return ledger;
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

    static async Task<FixtureState> Fixture(AccessReviewSystemLedger ledger,
        DateTimeOffset? recordedAt = null)
    {
        var pending = new AggregateScenario<AccessReviewSystemLedger>(ledger).PendingEvents;
        var attached = pending.ToArray();
        if (recordedAt is { } occurrence)
            for (var index = 0; index < attached.Length; index++)
            {
                var original = attached[index];
                attached[index] = original switch
                {
                    AccessExpectationProposed proposed => new AccessExpectationProposed(
                        proposed.TenantId, proposed.SystemInstanceId, proposed.Revision,
                        proposed.ProposerMemberId, proposed.Expectation),
                    AccessExpectationApproved approved => new AccessExpectationApproved(
                        approved.TenantId, approved.SystemInstanceId, approved.Revision,
                        approved.ExpectationId, approved.ApprovedBy, approved.ApprovedAt,
                        approved.SeparationOfDutiesWaiverId),
                    AccessExpectationExceptionRecorded exceptionRecorded =>
                        new AccessExpectationExceptionRecorded(exceptionRecorded.TenantId,
                            exceptionRecorded.SystemInstanceId, exceptionRecorded.Revision,
                            exceptionRecorded.Exception),
                    AccessPopulationExceptionRecorded populationExceptionRecorded =>
                        new AccessPopulationExceptionRecorded(populationExceptionRecorded.TenantId,
                            populationExceptionRecorded.SystemInstanceId,
                            populationExceptionRecorded.Revision,
                            populationExceptionRecorded.Exception),
                    _ => throw new InvalidOperationException(
                        "The access expectation fixture produced an unknown event."),
                };
                attached[index].AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
                    SystemInstanceId, (ulong)index + 1, occurrence));
            }
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(TenantId.ToString(), AccessReviewSystemLedger.Area,
            SystemInstanceId.ToString());
        await events.AppendAsync(stream, 0, attached);
        var replayed = new AggregateScenario<AccessReviewSystemLedger>(
            new AccessReviewSystemLedger(TenantId, SystemInstanceId)).Given(attached).Aggregate;
        var contributor = new AccessExpectationsTypeIBaselineSourceContributor(
            new AggregateReader(replayed), events);
        return new FixtureState(contributor, Context());
    }

    sealed record FixtureState(ITypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class AggregateReader(AccessReviewSystemLedger ledger) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(ledger.Id == aggregate.Id && ledger.GetType() == typeof(TAggregate)
                ? (TAggregate)(Aggregate)ledger
                : aggregate);
    }
}
