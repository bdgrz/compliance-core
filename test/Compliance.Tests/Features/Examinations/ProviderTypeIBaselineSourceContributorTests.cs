using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class ProviderTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ProviderId = Uuid.CreateVersion4();
    static readonly Uuid ServiceId = Uuid.CreateVersion4();
    static readonly Uuid OwnerPersonId = Uuid.CreateVersion4();
    static readonly DateOnly TypeIDate = new(2027, 3, 31);
    static readonly DateTimeOffset CreatedAt = new(2027, 2, 1, 12, 0, 0,
        TimeSpan.Zero);
    static readonly DateTimeOffset RevisedAfterTypeI = new(2027, 4, 1, 12, 0, 0,
        TimeSpan.Zero);

    [Fact]
    public async Task ShouldCaptureProviderAtTypeIDateGivenLaterRevision()
    {
        // Arrange
        var provider = new ProviderRegister(TenantId);
        var initial = Content("Payroll Processor");
        Assert.True(provider.Record(ProviderId, Uuid.CreateVersion4(), initial,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), CreatedAt).IsSuccess);
        Assert.True(provider.Revise(ProviderId, Uuid.CreateVersion4(), 1,
            Content("Renamed Payroll Processor"),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"),
            RevisedAfterTypeI).IsSuccess);
        var assuranceRegister = new ProviderAssuranceRegister(TenantId);
        var reportId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer");
        var assuranceRecordedAt = new DateTimeOffset(TypeIDate.ToDateTime(
            new TimeOnly(18, 0), DateTimeKind.Utc));
        var reportContent = new AssuranceReportContent("soc2_type2", "Example Assurance LLP",
            "Payroll processing", TypeIDate, TypeIDate.AddYears(-1), "unqualified",
            "Independent report, section I", ["Payroll"], [], [], [], null,
            new ProviderSourceCitation("soc2_report", "Payroll report", "v1", "report/1",
                "internal"));
        Assert.True(assuranceRegister.RecordReport(reportId, ProviderId,
            Uuid.CreateVersion4(), reportContent, 1, actor, assuranceRecordedAt).IsSuccess);
        var reviewContent = new ProviderReviewContent(TypeIDate, TypeIDate.AddYears(1),
            "soc2_type2", "acceptable", "The report supports the due-diligence conclusion.",
            reportId, [], null);
        Assert.True(assuranceRegister.RecordReview(Uuid.CreateVersion4(), ProviderId,
            Uuid.CreateVersion4(), reviewContent, 1, "material", actor,
            assuranceRecordedAt.AddHours(1)).IsSuccess);
        var gapId = Uuid.CreateVersion4();
        var gapContent = new ProviderCoverageGapContent(ServiceId, "Payroll processing",
            "Availability coverage", TypeIDate, TypeIDate, "assurance_report", reportId, 1,
            "A separate payroll service remains uncovered.");
        Assert.True(assuranceRegister.RecordCoverageGap(gapId, ProviderId,
            Uuid.CreateVersion4(), 1, gapContent, actor,
            assuranceRecordedAt.AddHours(2)).IsSuccess);

        var store = new InMemoryEventStore();
        var providerEvents = new AggregateScenario<ProviderRegister>(provider).PendingEvents;
        var stream = new EventStreamAddress(TenantId.ToString(), ProviderRegister.Area,
            TenantId.ToString());
        await store.AppendAsync(stream, 0, providerEvents.ToArray());
        var assuranceStream = new EventStreamAddress(TenantId.ToString(),
            ProviderAssuranceRegister.Area, TenantId.ToString());
        var assuranceEvents = new AggregateScenario<ProviderAssuranceRegister>(assuranceRegister)
            .PendingEvents.ToArray();
        await store.AppendAsync(assuranceStream, 0, assuranceEvents);
        var providerDirectory = new ProviderDirectory(provider.Get(ProviderId)!,
            await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                ProviderRegister.Area)));
        var assuranceDirectory = new AssuranceDirectory(
            await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                ProviderAssuranceRegister.Area)));
        var firstReport = assuranceRegister.Reports(ProviderId).Single();
        assuranceDirectory.SetCurrent(assuranceRegister, ProviderId, [firstReport]);
        var contributor = new ProviderTypeIBaselineSourceContributor(providerDirectory,
            assuranceDirectory, new AggregateReader(provider, assuranceRegister), store,
            new ProviderCsocReader());

        var staleProviderContributor = new ProviderTypeIBaselineSourceContributor(
            new ProviderDirectory(provider.Get(ProviderId)!,
                new ProjectionCheckpoint(EventCursor.Start)), assuranceDirectory,
            new AggregateReader(provider, assuranceRegister), store, new ProviderCsocReader());
        var staleProviderResult = await staleProviderContributor.ReadAsync(Engagement(),
            CancellationToken.None);
        Assert.False(staleProviderResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, staleProviderResult.Error.Kind);
        Assert.True(staleProviderResult.Error.IsTransient);

        var staleAssuranceDirectory = new AssuranceDirectory(
            new ProjectionCheckpoint(EventCursor.Start));
        staleAssuranceDirectory.SetCurrent(assuranceRegister, ProviderId, [firstReport]);
        var staleAssuranceContributor = new ProviderTypeIBaselineSourceContributor(
            providerDirectory, staleAssuranceDirectory,
            new AggregateReader(provider, assuranceRegister), store, new ProviderCsocReader());
        var staleAssuranceResult = await staleAssuranceContributor.ReadAsync(Engagement(),
            CancellationToken.None);
        Assert.False(staleAssuranceResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, staleAssuranceResult.Error.Kind);
        Assert.True(staleAssuranceResult.Error.IsTransient);

        // Act
        var result = await contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("providers", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("provider", source.Kind);
        Assert.Equal(ProviderId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(64, source.ContentSha256.Length);
        Assert.Contains(result.Value.Blockers, blocker =>
            blocker.Contains("open coverage gap", StringComparison.Ordinal));

        var revisedReport = reportContent with { Issuer = "Updated Assurance LLP" };
        Assert.True(assuranceRegister.ReviseReport(reportId, ProviderId,
            Uuid.CreateVersion4(), 1, revisedReport, 2, actor,
            RevisedAfterTypeI).IsSuccess);
        var closure = new ProviderCoverageGapClosureContent("coverage_restored",
            "assurance_report", reportId, 2,
            "The updated report now supports the missing service interval.");
        Assert.True(assuranceRegister.CloseCoverageGap(gapId, ProviderId,
            Uuid.CreateVersion4(), 1, closure, actor, RevisedAfterTypeI.AddHours(1)).IsSuccess);
        var allAssuranceEvents = new AggregateScenario<ProviderAssuranceRegister>(assuranceRegister)
            .PendingEvents.ToArray();
        await store.AppendAsync(assuranceStream, (ulong)assuranceEvents.Length,
            allAssuranceEvents.Skip(assuranceEvents.Length).ToArray());
        var currentReport = assuranceRegister.Reports(ProviderId).Single();
        assuranceDirectory.SetCheckpoint(await CheckpointAfterAsync(store,
            EventStreamPattern.ForPattern(TenantId.ToString(),
                ProviderAssuranceRegister.Area)));
        assuranceDirectory.SetCurrent(assuranceRegister, ProviderId,
            [firstReport, currentReport]);

        var laterResult = await contributor.ReadAsync(Engagement(), CancellationToken.None);
        Assert.True(laterResult.IsSuccess, laterResult.Error?.Message);
        Assert.Equal(source.ContentSha256, Assert.Single(laterResult.Value.Sources).ContentSha256);
        Assert.Contains(laterResult.Value.Blockers, blocker =>
            blocker.Contains("open coverage gap", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldRequireCapturedCsocGivenCarvedOutSubserviceProvider()
    {
        // Arrange
        var provider = new ProviderRegister(TenantId);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
        Assert.True(provider.Record(ProviderId, Uuid.CreateVersion4(),
            Content("Payroll subservice", subservice: true), actor, CreatedAt).IsSuccess);
        var store = new InMemoryEventStore();
        var stream = new EventStreamAddress(TenantId.ToString(), ProviderRegister.Area,
            TenantId.ToString());
        await store.AppendAsync(stream, 0,
            new AggregateScenario<ProviderRegister>(provider).PendingEvents.ToArray());
        var providerDirectory = new ProviderDirectory(provider.Get(ProviderId)!,
            await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                ProviderRegister.Area)));
        var assuranceDirectory = new AssuranceDirectory(new ProjectionCheckpoint(EventCursor.Start));
        var assuranceRegister = new ProviderAssuranceRegister(TenantId);
        var aggregateReader = new AggregateReader(provider, assuranceRegister);

        // Act
        var missingCsoc = await new ProviderTypeIBaselineSourceContributor(providerDirectory,
            assuranceDirectory, aggregateReader, store, new ProviderCsocReader())
            .ReadAsync(Engagement(), CancellationToken.None);
        var capturedCsoc = await new ProviderTypeIBaselineSourceContributor(providerDirectory,
            assuranceDirectory, aggregateReader, store, new ProviderCsocReader(ProviderId))
            .ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(missingCsoc.IsSuccess, missingCsoc.Error?.Message);
        Assert.Contains(missingCsoc.Value.Blockers, blocker =>
            blocker.Contains("no effective, applicable CSOC", StringComparison.Ordinal));
        Assert.True(capturedCsoc.IsSuccess, capturedCsoc.Error?.Message);
        Assert.DoesNotContain(capturedCsoc.Value.Blockers, blocker =>
            blocker.Contains("no effective, applicable CSOC", StringComparison.Ordinal));
        Assert.DoesNotContain(capturedCsoc.Value.Blockers, blocker =>
            blocker.Contains("unresolved facts", StringComparison.Ordinal) &&
            blocker.Contains("csocs", StringComparison.Ordinal));
    }

    static ProviderContent Content(string name, bool subservice = false)
    {
        var citation = new ProviderSourceCitation("contract", "Payroll agreement", "v1",
            "contract/1", "internal");
        var dependency = new ProviderDependency("client_service", ServiceId, ProgramId, null,
            "Processes payroll for the in-scope service.", CreatedAt,
            SourceCitation: citation)
        { SourceRevision = 1, ProgramRevision = 1 };
        return new ProviderContent(name, "payroll_processor", "material", ["customer_data"],
            "Processes customer data.", Subservice: subservice,
            BoundaryTreatment: subservice ? "carve_out" : null, OwnerPersonId: OwnerPersonId,
            OwnerReference: "Finance Operations", Dependencies: [dependency],
            SourceCitation: citation);
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", TypeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1,
        new DateTimeOffset(TypeIDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
        Uuid.CreateVersion4(), "Examiner");

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore store,
        EventStreamPattern pattern)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in store.ReadAsync(pattern, cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class AggregateReader(params Aggregate[] aggregates) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregates.FirstOrDefault(item => item.Id == aggregate.Id &&
                item.GetType() == typeof(TAggregate)) is TAggregate found ? found : aggregate);
    }

    sealed class ProviderCsocReader(params Uuid[] providerIds) : IProviderCsocBaselineReader
    {
        readonly HashSet<Uuid> _providerIds = providerIds.ToHashSet();

        public ValueTask<Result<IReadOnlySet<Uuid>>> ReadEffectiveProviderCsocsAsync(
            ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
            CancellationToken ct) => ValueTask.FromResult(
            Result<IReadOnlySet<Uuid>>.Success(_providerIds));
    }

    sealed class ProviderDirectory(ProviderView provider, ProjectionCheckpoint checkpoint)
        : IProviderReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<ProviderView?> GetAsync(Uuid tenantId, Uuid providerId,
            CancellationToken ct = default) => ValueTask.FromResult<ProviderView?>(
            tenantId == provider.TenantId && providerId == provider.ProviderId ? provider : null);

        public ValueTask<ProviderView?> GetRevisionAsync(Uuid tenantId, Uuid providerId,
            long revision, CancellationToken ct = default) => ValueTask.FromResult<ProviderView?>(
            tenantId == provider.TenantId && providerId == provider.ProviderId &&
            revision == provider.Revision ? provider : null);

        public ValueTask<Page<ProviderView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ProviderView>(
            tenantId == provider.TenantId ? [provider] : [], null));

        public ValueTask<Page<ProviderView>> ListRevisionsAsync(Uuid tenantId, Uuid providerId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProviderView>([], null));
    }

    sealed class AssuranceDirectory(ProjectionCheckpoint checkpoint) : IAssuranceReader
    {
        ProjectionCheckpoint _checkpoint = checkpoint;
        AssuranceReportView[] _reports = [];
        AssuranceReportView[] _reportRevisions = [];
        ProviderReviewView[] _reviews = [];
        ProviderCoverageGapView[] _gaps = [];

        public void SetCheckpoint(ProjectionCheckpoint value) => _checkpoint = value;

        public void SetCurrent(ProviderAssuranceRegister register, Uuid providerId,
            AssuranceReportView[] reportRevisions)
        {
            _reports = register.Reports(providerId).ToArray();
            _reportRevisions = reportRevisions;
            _reviews = register.Reviews(providerId).ToArray();
            _gaps = register.CoverageGaps(providerId).ToArray();
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(_checkpoint);

        public ValueTask<AssuranceReportView?> GetReportRevisionAsync(Uuid tenantId,
            Uuid reportId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult(_reportRevisions.SingleOrDefault(view =>
                view.TenantId == tenantId && view.ReportId == reportId &&
                view.Revision == revision));

        public ValueTask<Page<AssuranceReportView>> ListReportsAsync(Uuid tenantId,
            Uuid providerId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<AssuranceReportView>(_reports.Where(view =>
                view.TenantId == tenantId && view.ProviderId == providerId).ToArray(), null));

        public ValueTask<Page<ProviderReviewView>> ListReviewsAsync(Uuid tenantId,
            Uuid providerId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProviderReviewView>(_reviews.Where(view =>
                view.TenantId == tenantId && view.ProviderId == providerId).ToArray(), null));

        public ValueTask<ProviderCoverageGapView?> GetCoverageGapAsync(Uuid tenantId,
            Uuid gapId, CancellationToken ct = default) => ValueTask.FromResult(_gaps
            .SingleOrDefault(view => view.TenantId == tenantId && view.GapId == gapId));

        public ValueTask<Page<ProviderCoverageGapView>> ListCoverageGapsAsync(Uuid tenantId,
            Uuid providerId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProviderCoverageGapView>(_gaps.Where(view =>
                view.TenantId == tenantId && view.ProviderId == providerId).ToArray(), null));
    }
}
