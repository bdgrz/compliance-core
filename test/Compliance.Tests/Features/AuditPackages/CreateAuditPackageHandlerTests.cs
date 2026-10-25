using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Examinations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackageRules;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;
using TypeIIPeriodAggregate = Bdgrz.Compliance.Features.Operations.TypeIIPeriodRules;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class CreateAuditPackageHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCreatePackageFromExactFrozenTypeIBaselineGivenPersonalActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedTypeIBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        var command = fixture.Command(packageId, baseline);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            command), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.Current!.Status);
        Assert.Equal(baseline.TypeIBaselineId, saved.Current.Versions[0].SourceSnapshotId);
        Assert.Equal(baseline.ContentSha256, saved.Current.Versions[0].SourceSnapshotSha256);
        var selected = Assert.Single(saved.Current.Versions[0].SelectedContent);
        Assert.Equal(fixture.SourceRecordId, selected.SourceRecordId);
        Assert.Equal(fixture.SourceRevision, selected.SourceVersion);
        Assert.Equal(fixture.SourceSha256, selected.ContentSha256);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId),
            saved.Current.CreatedByMemberId);
    }

    [Fact]
    public async Task ShouldRejectSelectionOutsideFrozenBaselineGivenCreateCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedTypeIBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        var command = fixture.Command(packageId, baseline) with
        {
            Content = fixture.Content(baseline) with
            {
                SelectedContent = [fixture.Selection() with
                {
                    SourceRecordId = Uuid.CreateVersion4(),
                }],
            },
        };

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            command), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRequireOwningPeriodIdGivenTypeIICloseSnapshot()
    {
        // Arrange
        await using var fixture = new Fixture();
        var packageId = Uuid.CreateVersion4();
        var command = fixture.Command(packageId, await fixture.SeedTypeIBaselineAsync()) with
        {
            Content = fixture.Content(await fixture.GetBaselineAsync()) with
            {
                PackageKind = "type_ii",
                SourceSnapshotKind = "type_ii_close_snapshot",
            },
        };

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            command), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Null(saved.Current);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenCreateCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedTypeIBaselineAsync();
        var command = fixture.Command(Uuid.CreateVersion4(), baseline);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.SystemContext(command),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldCreateTypeIIPackageFromLatestFrozenCloseGivenExactSourceVersion()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = await fixture.SeedTypeIICloseAsync();
        var packageId = Uuid.CreateVersion4();
        var content = new AuditPackageContent(fixture.ExaminationId, "type_ii",
            "type_ii_close_snapshot", close.CloseSnapshotId, close.ContentSha256,
            "SOC 2 Type II", [fixture.TypeIISelection()], fixture.TypeIIPeriodId);
        var request = new CreateAuditPackage(fixture.TenantId, fixture.ProgramId, packageId,
            content);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId,
            request), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var version = Assert.Single(saved.Current!.Versions);
        Assert.Equal(fixture.TypeIIPeriodId, version.SourcePeriodId);
        Assert.Equal(close.CloseSnapshotId, version.SourceSnapshotId);
        Assert.Equal(fixture.SourceVersionId,
            Assert.Single(version.SelectedContent).SourceVersionId);
    }

    [Fact]
    public async Task ShouldReplayCreationGivenLostResponseAndLaterRetryTime()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedTypeIBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        var command = fixture.Command(packageId, baseline);
        var first = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId, command), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var retryOperations = new AuditPackageRootOperation(fixture.Reader, fixture.Packages,
            new NativeOperationExecutor(fixture.Reader, fixture.Executor, fixture.ContentStore), fixture.ContentStore,
            new FixedTimeProvider(Now.AddDays(1)));
        var retryHandler = new CreateAuditPackageHandler(retryOperations,
            new AuditPackageSourceBuilder(fixture.Examinations, fixture.Periods), fixture.ContentStore);

        // Act
        var replay = await retryHandler.HandleAsync(Fixture.Context(fixture.UserId, command), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(Now, saved.Current!.CreatedAt);
    }

    [Fact]
    public async Task ShouldReplayCreationGivenEqualConcurrentCommitBetweenInitialReadAndPreparation()
    {
        // Arrange
        await using var fixture = new Fixture();
        var baseline = await fixture.SeedTypeIBaselineAsync();
        var packageId = Uuid.CreateVersion4();
        var command = fixture.Command(packageId, baseline);
        var raced = false;
        var reader = new InterleavedPackageReader(fixture.Reader, async () =>
        {
            var winner = await fixture.Handler().HandleAsync(Fixture.Context(fixture.UserId, command),
                CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            raced = true;
        });
        var packages = new AuditPackageReader(reader, fixture.ContentStore, fixture.PeriodEvents);
        var operations = new AuditPackageRootOperation(reader, packages,
            new NativeOperationExecutor(reader, fixture.Executor, fixture.ContentStore), fixture.ContentStore,
            new FixedTimeProvider(Now));
        var handler = new CreateAuditPackageHandler(operations, new AuditPackageSourceBuilder(fixture.Examinations, fixture.Periods),
            fixture.ContentStore);

        // Act
        var replay = await handler.HandleAsync(Fixture.Context(fixture.UserId, command), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(raced);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Single((await fixture.Packages.ReadAsync(fixture.TenantId, fixture.ProgramId, packageId,
            CancellationToken.None)).History.Facts.OfType<AuditPackageCreated>());
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now);
        TypeIBaselineView? _baseline;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid TypeIIPeriodId { get; } = Uuid.CreateVersion4();
        public Uuid TypeIIPlanId { get; } = Uuid.CreateVersion4();
        public Uuid SourceRecordId { get; } = Uuid.CreateVersion4();
        public Uuid SourceVersionId { get; } = Uuid.CreateVersion4();
        public string SourceSha256 { get; } = new('b', 64);
        public long SourceRevision { get; } = 3;
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public InMemoryEventStore PeriodEvents { get; } = new();
        public TypeIIPeriodReader Periods => new(Reader, PeriodEvents, ContentStore);
        public ExaminationEngagementReader Examinations => new(Reader, PeriodEvents, ContentStore);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public IImmutableContentStore ContentStore { get; } =
            new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        public AuditPackageReader Packages => new(Reader, ContentStore, PeriodEvents);
        public AuditPackageRootOperation RootOperation => new(Reader, Packages,
            new NativeOperationExecutor(Reader, Executor, ContentStore), ContentStore, _clock);
        public async Task<AuditPackageRules> ReadPackageAsync(Uuid id) =>
            new(TenantId, ProgramId, id, (await Packages.ReadAsync(TenantId, ProgramId, id, CancellationToken.None)).View);
        public async Task<long> ManifestRevisionAsync(Uuid id) =>
            (await Packages.ReadAsync(TenantId, ProgramId, id, CancellationToken.None)).Owner.Revision;


        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(PeriodEvents);
            services.AddPortia();
            ExaminationSeeds.Configure(services, PeriodEvents);
            PackageSeeds.Configure(services, PeriodEvents);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public CreateAuditPackageHandler Handler() => new(RootOperation,
            new AuditPackageSourceBuilder(Examinations, Periods), ContentStore);

        public CreateAuditPackage Command(Uuid packageId, TypeIBaselineView baseline) =>
            new(TenantId, ProgramId, packageId, Content(baseline));

        public AuditPackageContent Content(TypeIBaselineView baseline) => new(ExaminationId,
            "type_i", "type_i_baseline", baseline.TypeIBaselineId, baseline.ContentSha256,
            "SOC 2 Type I", [Selection()]);

        public AuditPackageSelectionEntry Selection() => new("system_description",
            "system-description-1", "system_description", SourceRecordId, SourceRevision,
            "json", SourceSha256, 128, "management", SourceVersionId);

        public AuditPackageSelectionEntry TypeIISelection() => new("control_matrix",
            "control-17", "control", SourceRecordId, SourceRevision, "json", SourceSha256,
            128, "management", SourceVersionId);

        public async Task<TypeIBaselineView> SeedTypeIBaselineAsync()
        {
            var engagement = new ExaminationEngagementRules(TenantId, ProgramId, ExaminationId);
            var memberId = RbacIds.Member(TenantId, UserId);
            var boundaryId = Uuid.CreateVersion4();
            Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i",
                "Independent Audit LLP", new DateOnly(2026, 9, 30), null, ["security"],
                boundaryId, [memberId]), memberId, "Engagement lead", Now));
            var preview = TypeIBaselinePreviewTestFactory.Create(TenantId, ProgramId, ExaminationId,
            [new TypeIBaselineSourceReference("approved_boundary", boundaryId,
                Uuid.CreateVersion4(), 1, SourceSha256),
             new TypeIBaselineSourceReference("system_description", SourceRecordId,
                SourceVersionId, SourceRevision, SourceSha256)]);
            Assert.Null(engagement.FreezeBaseline(engagement.Revision, BaselineId, preview,
                "Confirmed the exact Type I source snapshot.", memberId, "Engagement lead",
                Now.AddMinutes(1)));
            _baseline = engagement.Baseline;
            await ExaminationSeeds.WriteAsync(Reader, Writer, PeriodEvents, ContentStore, engagement);
            return _baseline!;
        }

        public async Task<TypeIIPeriodCloseSnapshotView> SeedTypeIICloseAsync()
        {
            var memberId = RbacIds.Member(TenantId, UserId);
            var reviewerMemberId = RbacIds.Member(TenantId, Uuid.CreateVersion4());
            var executiveMemberId = RbacIds.Member(TenantId, Uuid.CreateVersion4());
            var plan = new TypeIIPlanAggregate(TenantId, ProgramId, TypeIIPlanId);
            Assert.Null(plan.Propose(CreateTypeIIPlanContent(), memberId, "Compliance lead",
                Now.AddDays(-3)));
            Assert.Null(plan.Review(plan.Revision, 1, "approve", "Reviewed the controls and gaps.",
                reviewerMemberId, "Independent reviewer", Now.AddDays(-2)));
            Assert.Null(plan.Approve(plan.Revision, 1, "Approved the Type II scope.",
                executiveMemberId, "CISO", "Executive approver", Now.AddDays(-1)));

            var sourceReferences = TypeIIPeriodSourceReferences();
            var period = new TypeIIPeriodAggregate(TenantId, ProgramId, TypeIIPeriodId);
            Assert.Null(period.Start(plan.Current!, new TypeIIPeriodOpeningContent(ExaminationId,
                Uuid.CreateVersion4(), new string('c', 64), sourceReferences, [GapId]), memberId,
                "Compliance lead", Now));
            var occurrences = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId)
                .ToArray();
            Assert.Null(period.RecordCloseValidation(period.Revision,
                new TypeIIPeriodCloseValidationContent(sourceReferences, occurrences, occurrences,
                    [], [], []), reviewerMemberId, "Close validator", Now.AddMonths(12)));
            Assert.Null(period.ApproveClose(period.Revision, [], "Reviewed the exact close.",
                executiveMemberId, "Chief Executive", "Executive approver",
                Now.AddMonths(12).AddHours(1)));
            var closeSnapshotId = Uuid.CreateVersion4();
            Assert.Null(period.FreezeClose(period.Revision, new TypeIIPeriodCloseSnapshotContent(
                closeSnapshotId, sourceReferences, occurrences, []), executiveMemberId,
                "Chief Executive", Now.AddMonths(12).AddHours(2)));
            await TypeIIPeriodSeeds.WriteAsync(Writer, ContentStore, period);
            return period.Current!.CloseSnapshots[^1];
        }

        TypeIIPlanContent CreateTypeIIPlanContent() => new(ExaminationId, Uuid.CreateVersion4(),
            new string('a', 64), Uuid.CreateVersion4(), new DateOnly(2026, 10, 1),
            new DateOnly(2027, 9, 30), "Independent Audit LLP", ["security", "availability"],
            Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            [TypeIIReference("policy"), TypeIIReference("risk"), TypeIIReference("vendor"),
                TypeIIReference("finding"), TypeIIReference("change")],
            [new TypeIIControlPlanEntry(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                "Quarterly access review", Uuid.CreateVersion4(), "quarterly", 4,
                new DateOnly(2026, 10, 1), null,
                ExpectedEvidenceDescriptions: ["Dated review record"])],
            [new("finding", GapId, Uuid.CreateVersion4(), 1, new string('b', 64), "carry")],
            [new(GapId, "Open control gap", "acknowledged", "Owner and due date confirmed")],
            []);

        IReadOnlyList<ExaminationContentReference> TypeIIPeriodSourceReferences() =>
            [TypeIIReference("boundary"), TypeIIReference("inventory"),
             new ExaminationContentReference("control", SourceRecordId, SourceVersionId,
                 SourceRevision, SourceSha256), TypeIIReference("policy"),
             TypeIIReference("provider"), TypeIIReference("finding")];

        static ExaminationContentReference TypeIIReference(string kind) =>
            new(kind, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('d', 64));

        static Uuid GapId { get; } = Uuid.CreateVersion4();

        public Task<TypeIBaselineView> GetBaselineAsync() => Task.FromResult(_baseline!);

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
                new HttpInvocation("POST", "/api/v1/tenants/test/programs/test/audit-packages/test",
                    "/api/v1/tenants/test/programs/test/audit-packages/test",
                    "audit-package-create-test")));

        public static Cntryl.Portia.RequestContext<T> SystemContext<T>(T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(RequestActor.System,
                new HttpInvocation("POST", "/api/v1/tenants/test/programs/test/audit-packages/test",
                    "/api/v1/tenants/test/programs/test/audit-packages/test",
                    "audit-package-create-test")));

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "package-author@example.test")], "BdgrzSession"));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class InterleavedPackageReader(IAggregateReader inner, Func<Task> beforeSecondRead) : IAggregateReader
    {
        int _packageReads;
        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is AuditPackageManifest && Interlocked.Increment(ref _packageReads) == 2)
                await beforeSecondRead();
            return await inner.HydrateAsync(aggregate, ct);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
