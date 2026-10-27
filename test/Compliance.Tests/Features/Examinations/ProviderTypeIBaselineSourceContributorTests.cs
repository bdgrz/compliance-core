using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Readers;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

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
        var store = new InMemoryEventStore();
        await using var services = Services(store);
        var initial = Content("Payroll Processor");
        await NamedRecordFacts.SeedProviderAsync(services, TenantId, ProviderId, initial,
            MemberActor("Recorder"), CreatedAt);
        await NamedRecordFacts.SeedProviderAsync(services, TenantId, ProviderId, Content("Renamed Payroll Processor"),
            MemberActor("Recorder"), RevisedAfterTypeI);
        var provider = await new ProviderSource(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<IImmutableContentStore>()).GetAsync(TenantId, ProviderId, CancellationToken.None);
        var contentStore = services.GetRequiredService<IImmutableContentStore>();
        var content = contentStore;
        var reportId = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer");
        var assuranceRecordedAt = new DateTimeOffset(TypeIDate.ToDateTime(
            new TimeOnly(18, 0), DateTimeKind.Utc));
        var reportContent = new AssuranceReportContent("soc2_type2", "Example Assurance LLP",
            "Payroll processing", TypeIDate, TypeIDate.AddYears(-1), "unqualified",
            "Independent report, section I", ["Payroll"], [], [], [], null,
            new ProviderSourceCitation("soc2_report", "Payroll report", "v1", "report/1",
                "internal"));
        var reviewContent = new ProviderReviewContent(TypeIDate, TypeIDate.AddYears(1),
            "soc2_type2", "acceptable", "The report supports the due-diligence conclusion.",
            reportId, [], null);
        var gapContent = new ProviderCoverageGapContent(ServiceId, "Payroll processing",
            "Availability coverage", TypeIDate, TypeIDate, "assurance_report", reportId, 1,
            "A separate payroll service remains uncovered.");
        var report = new ProviderAssuranceReport(TenantId, ProviderId, reportId);
        var review = new ProviderAssuranceReview(TenantId, reviewId);
        var gap = new ProviderAssuranceGap(TenantId, ProviderId, gapId);
        Assert.True(report.Record(Provenance(report.Stream, reportId, reportId), await WriteAsync(content, reportContent), 1,
            actor, assuranceRecordedAt, await WriteAsync(content, reportContent), await WriteAsync(content, reportContent)).IsSuccess);
        Assert.True(review.Admit(Provenance(review.Stream, reviewId, reviewId), await WriteAsync(content, reviewContent),
            await WriteAsync(content, reviewContent)).IsSuccess);
        Assert.True(review.Complete(Provenance(review.Stream, Uuid.CreateVersion4(), reviewId),
            await WriteAsync(content, reviewContent), 1, "material", 1, actor, assuranceRecordedAt.AddHours(1), ProviderId,
            await WriteAsync(content, reviewContent)).IsSuccess);
        var gapAt = assuranceRecordedAt.AddHours(2);
        var gapRequest = new RecordProviderCoverageGap(gapId, TenantId, ProviderId, gapContent);
        var gapBody = await ProviderAssuranceContent.WriteAsync(content, TenantId, ProviderAssuranceContent.GapRecordedSchema,
            gapContent, CancellationToken.None);
        var gapResult = await ProviderAssuranceContent.WriteAsync(content, TenantId, ProviderAssuranceContent.GapRegistrationSchema,
            new ProviderCoverageGapRegistration(gapId, 1), CancellationToken.None);
        var gapResponse = await FirstResponses.FreezeAsync(content, TenantId.ToString(),
            ProviderAssuranceContent.Completed(gapResult, ProviderAssuranceContent.GapRegistrationSchema));
        Assert.True(gap.Record(gap.Provenance(gapId, nameof(RecordProviderCoverageGap), actor, "initial", null,
                AuthoredIntentDigest.Compute(gapRequest), gapAt), 1, gapBody, actor, gapAt, gapResponse, gapResult).IsSuccess);

        await AppendAsync(store, report, 0);
        await AppendAsync(store, review, 0);
        await AppendAsync(store, gap, 0);
        var providerDirectory = new ProviderDirectory(provider!,
            await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                Provider.Area)));
        var assuranceDirectory = new AssuranceDirectory(
            await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                ProviderAssuranceStreams.Area)));
        var firstReport = ReportView(reportId, 1, reportContent, 1, actor, assuranceRecordedAt);
        var reviewView = new ProviderReviewView(TenantId, reviewId, ProviderId, reviewContent, 1, 1, actor,
            assuranceRecordedAt.AddHours(1));
        var openGap = new ProviderCoverageGapView(TenantId, gapId, ProviderId, 1, 1, gapContent, "open", actor,
            assuranceRecordedAt.AddHours(2), null, []);
        assuranceDirectory.SetCurrent([firstReport], [firstReport], [reviewView], [openGap]);
        var contributor = new ProviderTypeIBaselineSourceContributor(providerDirectory,
            assuranceDirectory, new AggregateReader(services.GetRequiredService<IAggregateReader>(),
                report, review, gap), store, new ProviderCsocReader(), contentStore);

        var staleProviderContributor = new ProviderTypeIBaselineSourceContributor(
            new ProviderDirectory(provider!,
                new ProjectionCheckpoint(EventCursor.Start)), assuranceDirectory,
            new AggregateReader(services.GetRequiredService<IAggregateReader>(), report, review, gap), store,
            new ProviderCsocReader(), contentStore);
        var staleProviderResult = await staleProviderContributor.ReadAsync(Engagement(),
            CancellationToken.None);
        Assert.False(staleProviderResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, staleProviderResult.Error.Kind);
        Assert.True(staleProviderResult.Error.IsTransient);

        var staleAssuranceDirectory = new AssuranceDirectory(
            new ProjectionCheckpoint(EventCursor.Start));
        staleAssuranceDirectory.SetCurrent([firstReport], [firstReport], [reviewView], [openGap]);
        var staleAssuranceContributor = new ProviderTypeIBaselineSourceContributor(
            providerDirectory, staleAssuranceDirectory,
            new AggregateReader(services.GetRequiredService<IAggregateReader>(), report, review, gap), store,
            new ProviderCsocReader(), contentStore);
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
        Assert.True(report.Revise(Provenance(report.Stream, Uuid.CreateVersion4(), Uuid.CreateVersion4()), 1,
            await WriteAsync(content, revisedReport), 2, actor, RevisedAfterTypeI,
            await WriteAsync(content, revisedReport), await WriteAsync(content, revisedReport)).IsSuccess);
        var closure = new ProviderCoverageGapClosureContent("coverage_restored",
            "assurance_report", reportId, 2,
            "The updated report now supports the missing service interval.");
        var closeId = Uuid.CreateVersion4();
        var closedAt = RevisedAfterTypeI.AddHours(1);
        gap = await services.GetRequiredService<IAggregateReader>().HydrateAsync(
            new ProviderAssuranceGap(TenantId, ProviderId, gapId, closeId));
        var closeRequest = new CloseProviderCoverageGap(closeId, TenantId, ProviderId, gapId, 1, closure);
        var closureBody = await ProviderAssuranceContent.WriteAsync(content, TenantId,
            ProviderAssuranceContent.GapClosedSchema, closure, CancellationToken.None);
        var closureResult = await ProviderAssuranceContent.WriteAsync(content, TenantId,
            ProviderAssuranceContent.GapRegistrationSchema, new ProviderCoverageGapRegistration(gapId, 2),
            CancellationToken.None);
        var closureResponse = await FirstResponses.FreezeAsync(content, TenantId.ToString(),
            ProviderAssuranceContent.Completed(closureResult, ProviderAssuranceContent.GapRegistrationSchema));
        var closed = gap.Close(gap.Provenance(closeId, nameof(CloseProviderCoverageGap), actor, "current", 1,
                AuthoredIntentDigest.Compute(closeRequest), closedAt), 1, gapContent, closure, closureBody, actor,
            closedAt, closureResponse, closureResult);
        Assert.True(closed.IsSuccess, closed.Error?.Message);
        await AppendAsync(store, report, 1);
        await store.AppendAsync(gap.Stream, 1, new AggregateScenario<ProviderAssuranceGap>(gap).PendingEvents.ToArray());
        var currentReport = ReportView(reportId, 2, revisedReport, 2, actor, RevisedAfterTypeI);
        var closedGap = openGap with
        {
            Revision = 2,
            Status = "closed",
            Closure = new ProviderCoverageGapClosureView(closure, actor, RevisedAfterTypeI.AddHours(1)) { Revision = 2 },
        };
        assuranceDirectory.SetCheckpoint(await CheckpointAfterAsync(store,
            EventStreamPattern.ForPattern(TenantId.ToString(),
                ProviderAssuranceStreams.Area)));
        assuranceDirectory.SetCurrent([currentReport], [firstReport, currentReport], [reviewView], [closedGap]);

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
        var store = new InMemoryEventStore();
        await using var services = Services(store);
        await NamedRecordFacts.SeedProviderAsync(services, TenantId, ProviderId,
            Content("Payroll subservice", subservice: true), MemberActor("Recorder"), CreatedAt);
        var contentStore = services.GetRequiredService<IImmutableContentStore>();
        var provider = await new ProviderSource(services.GetRequiredService<IAggregateReader>(), contentStore)
            .GetAsync(TenantId, ProviderId, CancellationToken.None);
        var providerDirectory = new ProviderDirectory(provider!,
            await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                Provider.Area)));
        var assuranceDirectory = new AssuranceDirectory(new ProjectionCheckpoint(EventCursor.Start));
        var aggregateReader = new AggregateReader(services.GetRequiredService<IAggregateReader>());

        // Act
        var missingCsoc = await new ProviderTypeIBaselineSourceContributor(providerDirectory,
            assuranceDirectory, aggregateReader, store, new ProviderCsocReader(), contentStore)
            .ReadAsync(Engagement(), CancellationToken.None);
        var capturedCsoc = await new ProviderTypeIBaselineSourceContributor(providerDirectory,
            assuranceDirectory, aggregateReader, store, new ProviderCsocReader(ProviderId), contentStore)
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

    static OperationProvenance Provenance(EventStreamAddress owner, Uuid logicalId, Uuid rootId) => new(logicalId, rootId, null,
        "TestCommand", ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder"), owner, "initial", null,
        new string('a', 64), CreatedAt, owner, owner);

    static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, T value) where T : notnull =>
        content.WriteJsonAsync(TenantId.ToString(), Uuid.CreateVersion4(), "test-content.v1", value);

    static async Task AppendAsync(InMemoryEventStore store, Aggregate owner, int alreadyAppended)
    {
        var pending = owner switch
        {
            ProviderAssuranceReport report => new AggregateScenario<ProviderAssuranceReport>(report).PendingEvents,
            ProviderAssuranceReview review => new AggregateScenario<ProviderAssuranceReview>(review).PendingEvents,
            ProviderAssuranceGap gap => new AggregateScenario<ProviderAssuranceGap>(gap).PendingEvents,
            _ => throw new ArgumentException("Unsupported owner.", nameof(owner)),
        };
        await store.AppendAsync(owner.Stream, (ulong)alreadyAppended, pending.Skip(alreadyAppended).ToArray());
    }

    static AssuranceReportView ReportView(Uuid reportId, long revision, AssuranceReportContent content,
        long providerRevision, ActorReference actor, DateTimeOffset recordedAt) => new(TenantId, reportId, ProviderId,
        revision, content, providerRevision, content.Exceptions?.Count ?? 0, content.ComplementaryControls?.Count ?? 0,
        content.CoverageGaps?.Count ?? 0, actor, recordedAt);

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

    static ActorReference MemberActor(string display) => ActorReference.ForMember(Uuid.CreateVersion4(), display);

    static ServiceProvider Services(InMemoryEventStore store)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IEventStore>(store);
        collection.AddSingleton<IDomainEventReader>(store);
        collection.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
            new PersistenceKvClient(new InMemoryKvClient())));
        collection.AddPortia();
        collection.AddVerifiedProviderGapReader();
        return collection.BuildServiceProvider();
    }

    /// <summary>Hydrates providers and gaps from their verified real stores; the other owner fixtures retain their fixed instances.</summary>
    sealed class AggregateReader(IAggregateReader real, params Aggregate[] aggregates) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            aggregate is Provider or ProviderAssuranceGap
                ? real.HydrateAsync(aggregate, ct)
                : ValueTask.FromResult(aggregates.FirstOrDefault(item => item.Id == aggregate.Id &&
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

        public void SetCurrent(AssuranceReportView[] reports, AssuranceReportView[] reportRevisions,
            ProviderReviewView[] reviews, ProviderCoverageGapView[] gaps)
        {
            _reports = reports;
            _reportRevisions = reportRevisions;
            _reviews = reviews;
            _gaps = gaps;
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(_checkpoint);

        public ValueTask<AssuranceReportView?> GetReportRevisionAsync(Uuid tenantId, Uuid providerId,
            Uuid reportId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult(_reportRevisions.SingleOrDefault(view =>
                view.TenantId == tenantId && view.ProviderId == providerId && view.ReportId == reportId &&
                view.Revision == revision));

        public ValueTask<Page<AssuranceReportView>> ListReportsAsync(Uuid tenantId,
            Uuid providerId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<AssuranceReportView>(_reports.Where(view =>
                view.TenantId == tenantId && view.ProviderId == providerId).ToArray(), null));

        public ValueTask<ProviderReviewView?> GetReviewAsync(Uuid tenantId, Uuid reviewId,
            CancellationToken ct = default) => ValueTask.FromResult(_reviews
            .SingleOrDefault(view => view.TenantId == tenantId && view.ReviewId == reviewId));

        public ValueTask<Page<ProviderReviewView>> ListReviewsAsync(Uuid tenantId,
            Uuid providerId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProviderReviewView>(_reviews.Where(view =>
                view.TenantId == tenantId && view.ProviderId == providerId).ToArray(), null));

        public ValueTask<ProviderCoverageGapView?> GetCoverageGapAsync(Uuid tenantId, Uuid providerId,
            Uuid gapId, CancellationToken ct = default) => ValueTask.FromResult(_gaps
            .SingleOrDefault(view => view.TenantId == tenantId && view.ProviderId == providerId &&
                view.GapId == gapId));

        public ValueTask<Page<ProviderCoverageGapView>> ListCoverageGapsAsync(Uuid tenantId,
            Uuid providerId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ProviderCoverageGapView>(_gaps.Where(view =>
                view.TenantId == tenantId && view.ProviderId == providerId).ToArray(), null));
    }
}
