using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class ApplicationTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ApplicationId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid RequesterId = Uuid.CreateVersion4();
    static readonly DateTimeOffset DayOne = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly string[] ScheduledRetirementSourceKinds =
        ["application_revision", "application_retirement"];

    [Fact]
    public async Task ShouldCaptureRevisionEffectiveOnTypeIDateGivenLaterApplicationRevision()
    {
        // Arrange
        var fixture = Fixture([Revision(1, "Payroll", DayOne, unresolved: ["owner_reference"]),
            Revision(2, "Payroll successor", DayOne.AddDays(30))]);
        var engagement = Engagement(new DateOnly(2026, 6, 30));

        // Act
        var result = await fixture.Contributor.ReadAsync(engagement, fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("applications", result.Value.Family);
        Assert.Equal("application_revision", source.Kind);
        Assert.Equal(ApplicationId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(ApplicationId, "application_revision_v1:1"),
            source.VersionId);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("owner_reference", StringComparison.Ordinal));
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldForbidRestrictedApplicationGivenRequesterCannotReadIt()
    {
        // Arrange
        var fixture = Fixture([Revision(1, "Payroll", DayOne, restricted: true)],
            restrictedReadAllowed: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(new DateOnly(2026, 6, 30)),
            fixture.Context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.DoesNotContain(ApplicationId.ToString(), result.Error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldCaptureImportedApplicationRevisionGivenApprovedBoundaryInScope()
    {
        // Arrange
        var fixture = Fixture([Revision(1, "Payroll", DayOne, changeKind: "imported")]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(new DateOnly(2026, 6, 30)),
            fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("application_revision", Assert.Single(result.Value.Sources).Kind);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldCaptureScheduledRetirementGivenRetirementRecordedByTypeIDate()
    {
        // Arrange
        var retirementAt = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var fixture = Fixture([Revision(1, "Payroll", DayOne),
            Revision(2, "Payroll", DayOne.AddDays(5), changeKind: "retired",
                retirement: new RetirementView(retirementAt, "Migration planned", null))]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(new DateOnly(2026, 6, 30)),
            fixture.Context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ScheduledRetirementSourceKinds,
            result.Value.Sources.Select(static source => source.Kind));
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("scheduled after the Type I date", StringComparison.Ordinal));
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldRejectChangedBoundaryVersionGivenPriorPreviewReference()
    {
        // Arrange
        var fixture = Fixture([Revision(1, "Payroll", DayOne)]);
        var wrong = fixture.Context.PreviouslyCapturedSources[0] with
        {
            ContentSha256 = new string('f', 64),
        };
        var context = fixture.Context with { PreviouslyCapturedSources = [wrong] };

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(new DateOnly(2026, 6, 30)),
            context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    static FixtureState Fixture(IReadOnlyList<ApplicationRevisionView> history,
        bool restrictedReadAllowed = true)
    {
        var events = new InMemoryEventStore();
        var source = Source(history);
        var aggregates = new AggregateReader(source);
        var entry = new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion", "application",
            ApplicationId.ToString(), ApplicationId, string.Empty, "Included application", false);
        var version = new BoundaryVersionView(TenantId, BoundaryId, ProgramId,
            Uuid.CreateVersion4(), 1,
            new BoundaryContent("In-scope system", "type_i",
                ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
                [entry]), "approved", new DateOnly(2026, 6, 1), AuthorId, "Examiner", DayOne);
        var boundary = new BoundaryView(TenantId, BoundaryId, ProgramId, null, version, null, 1);
        var boundaryDirectory = new BoundaryDirectory(boundary, version);
        var applications = new ApplicationDirectory(history);
        var applicationConsistency = new SystemInstanceReadConsistency(applications, aggregates,
            new LegacySystemInstanceSource(applications, events), events);
        var boundaryConsistency = new ProgramSetupWorkReadConsistency(new ProgramDirectory(),
            boundaryDirectory, events);
        var visibility = RestrictedApplicationVisibilityFixture.Create(aggregates,
            restrictedReadAllowed);
        var contributor = new ApplicationTypeIBaselineSourceContributor(applications, aggregates,
            visibility, applicationConsistency, boundaryDirectory, boundaryConsistency);
        var boundaryReference = new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
            version.VersionId, version.Revision, SnapshotContentIdentity.ApprovedBoundaryVersion(version));
        var context = new TypeIBaselineSourceReadContext(RequesterId, [boundaryReference]);
        return new FixtureState(contributor, context);
    }

    static DeclaredApplication Source(IReadOnlyList<ApplicationRevisionView> history)
    {
        var first = history[0];
        var source = new DeclaredApplication(TenantId, ApplicationId);
        Assert.True(source.Declare(first.Name, first.Purpose, first.OwnerReference, AuthorId,
            "Examiner", first.LastChangedAt, isRestricted: first.IsRestricted).IsSuccess);
        for (var index = 1; index < history.Count; index++)
        {
            var revision = history[index];
            if (revision.ChangeKind == "retired")
            {
                var retirement = Assert.IsType<RetirementView>(revision.Retirement);
                Assert.Null(source.Retire(source.Revision, retirement.EffectiveAt,
                    retirement.Reason, retirement.MergedIntoApplicationId, AuthorId,
                    "Examiner", revision.LastChangedAt));
                continue;
            }
            Assert.Null(source.Revise(source.Revision, revision.Name, revision.Purpose,
                revision.OwnerReference, AuthorId, "Examiner", revision.LastChangedAt,
                isRestricted: revision.IsRestricted));
        }
        return source;
    }

    static ApplicationRevisionView Revision(long revision, string name, DateTimeOffset changedAt,
        IReadOnlyList<string>? unresolved = null, bool restricted = false,
        string? changeKind = null, RetirementView? retirement = null) =>
        new(TenantId, ApplicationId, revision, name, "Process payroll", "Payroll team", "manual",
            "source-1", false, unresolved ?? [], AuthorId, "Examiner", changedAt,
            changeKind ?? (revision == 1 ? "declared" : "revised"), null, null)
        {
            IsRestricted = restricted,
            Lifecycle = retirement is null ? "active" : "retired",
            Retirement = retirement,
        };

    static ExaminationEngagementView Engagement(DateOnly periodStart) => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", periodStart, null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        BoundaryId, [], "draft", 1, DayOne, AuthorId, "Examiner");

    sealed record FixtureState(ApplicationTypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class AggregateReader(DeclaredApplication source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate is DeclaredApplication
                ? (TAggregate)(Aggregate)source : aggregate);
    }

    sealed class ApplicationDirectory(IReadOnlyList<ApplicationRevisionView> history)
        : IApplicationDirectoryReader
    {
        readonly ApplicationRevisionView _latest = history[^1];
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
        public ValueTask<ApplicationView?> GetAsync(Uuid tenantId, Uuid applicationId,
            CancellationToken ct = default) => ValueTask.FromResult<ApplicationView?>(
            tenantId == TenantId && applicationId == ApplicationId
                ? new ApplicationView(_latest.TenantId, _latest.ApplicationId, _latest.Revision,
                    _latest.Name, _latest.Purpose, _latest.OwnerReference, _latest.SourceKind,
                    _latest.SourceIdentifier, _latest.HasSystemInstances, _latest.Unresolved,
                    _latest.LastChangedByMemberId, _latest.LastChangedByDisplay,
                    _latest.LastChangedAt)
                {
                    IsRestricted = _latest.IsRestricted,
                    Lifecycle = _latest.Lifecycle,
                    Retirement = _latest.Retirement,
                }
                : null);
        public ValueTask<Page<ApplicationView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ApplicationView>([], null));
        public ValueTask<ApplicationRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid applicationId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult(history.FirstOrDefault(item => item.Revision == revision));
        public ValueTask<Page<ApplicationRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ApplicationRevisionView>?>(
                new Page<ApplicationRevisionView>(history, null));
        public ValueTask<SystemInstanceView?> GetInstanceAsync(Uuid tenantId, Uuid instanceId,
            CancellationToken ct = default) => ValueTask.FromResult<SystemInstanceView?>(null);
        public ValueTask<Page<SystemInstanceView>> ListInstancesAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<SystemInstanceView>([], null));
    }

    sealed class BoundaryDirectory(BoundaryView boundary, BoundaryVersionView version)
        : IBoundaryDirectoryReader
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
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class ProgramDirectory : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(null);
        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ProgramView>([], null));
        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(new Page<ProgramRevisionView>([], null));
        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) => ValueTask.FromResult<ProgramRevisionView?>(null);
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }
}
