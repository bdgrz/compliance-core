using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class FindingTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid FindingId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationItemId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateOnly TypeIDate = new(2026, 1, 31);
    static readonly DateTimeOffset RaisedAt = new(2026, 1, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset AfterTypeIDate = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCaptureAuditorFindingAsOfTypeIDateGivenLaterRevision()
    {
        // Arrange
        var withLaterRevision = await Fixture(includeLaterRevision: true);
        var atRaise = await Fixture(includeLaterRevision: false);

        // Act
        var laterResult = await withLaterRevision.Contributor.ReadAsync(Engagement(),
            withLaterRevision.Context, CancellationToken.None);
        var earlierResult = await atRaise.Contributor.ReadAsync(Engagement(), atRaise.Context,
            CancellationToken.None);

        // Assert
        Assert.True(laterResult.IsSuccess, laterResult.Error?.Message);
        Assert.True(earlierResult.IsSuccess, earlierResult.Error?.Message);
        Assert.Equal("findings", laterResult.Value.Family);
        var laterFinding = Assert.Single(laterResult.Value.Sources);
        var earlierFinding = Assert.Single(earlierResult.Value.Sources);
        Assert.Equal("finding", laterFinding.Kind);
        Assert.Equal(FindingId, laterFinding.RecordId);
        Assert.Equal(1, laterFinding.Revision);
        Assert.Equal(Uuid.CreateVersion5(FindingId, "finding_v1:1"), laterFinding.VersionId);
        Assert.Equal(earlierFinding.ContentSha256, laterFinding.ContentSha256);
        Assert.Matches("^[0-9a-f]{64}$", laterFinding.ContentSha256);
    }

    static async Task<FixtureState> Fixture(bool includeLaterRevision)
    {
        var raised = new FindingRaised(TenantId, ProgramId, FindingId, 1,
            new FindingSource("examination_item", ExaminationItemId, "item-v1",
                "The auditor's original wording is preserved."),
            "Auditor finding", "Original finding description.", "high", "Production controls",
            OwnerId, new DateOnly(2026, 2, 15), [],
            ActorReference.ForMember(MemberId, "Engagement lead"), RaisedAt);
        var sourceEvents = new List<DomainEvent>
        {
            DomainEventSeed.Attach(raised, ProgramId, 1,
                eventId: Uuid.CreateVersion5(FindingId, "finding_raised_v1"),
                occurredOn: RaisedAt),
        };
        if (includeLaterRevision)
            sourceEvents.Add(DomainEventSeed.Attach(new FindingRevised(TenantId, ProgramId,
                FindingId, 2, "critical", OwnerId, new DateOnly(2026, 3, 1),
                "Expanded after the cutoff", "Root cause", "Auditor supplied additional evidence.",
                ActorReference.ForMember(MemberId, "Engagement lead"), AfterTypeIDate), ProgramId,
                2, eventId: Uuid.CreateVersion5(FindingId, "finding_revised_v2"),
                occurredOn: AfterTypeIDate));

        var store = new InMemoryEventStore();
        var stream = new EventStreamAddress(TenantId.ToString(), "remediation",
            ProgramId.ToString());
        var attachedEvents = sourceEvents.ToArray();
        await store.AppendAsync(stream, 0, attachedEvents);
        var ledger = new AggregateScenario<RemediationLedger>(new RemediationLedger(TenantId,
            ProgramId)).Given(attachedEvents).Aggregate;
        var contributor = new FindingTypeIBaselineSourceContributor(store,
            new AggregateReader(ledger));
        return new FixtureState(contributor,
            new TypeIBaselineSourceReadContext(MemberId, []));
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", TypeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1,
        new DateTimeOffset(TypeIDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
        MemberId, "Examiner");

    sealed record FixtureState(FindingTypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class AggregateReader(RemediationLedger source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(source.Id == aggregate.Id && source.GetType() == typeof(TAggregate)
                ? (TAggregate)(Aggregate)source
                : aggregate);
    }
}
