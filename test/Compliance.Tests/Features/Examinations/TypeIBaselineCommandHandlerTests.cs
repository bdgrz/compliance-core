using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class TypeIBaselineCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldFreezeExactPreviewGivenConfirmation()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        fixture.SourceReader.Preview = fixture.PreviewWith(fixture.BoundarySource);
        var request = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            fixture.SourceReader.Preview.ContentSha256);

        // Act
        var result = await fixture.FreezeHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("baseline_frozen", saved.View?.Status);
        Assert.Equal(fixture.SourceReader.Preview.ContentSha256, saved.Baseline?.ContentSha256);
        Assert.Equal(fixture.SourceReader.Preview.SourceCoverage.OrderBy(static item => item.Family,
                StringComparer.Ordinal), saved.Baseline?.SourceCoverage);
    }

    [Fact]
    public async Task ShouldReplayEqualWinningFreezeGivenSourceFailureAfterNativeAcceptance()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        fixture.SourceReader.Preview = fixture.PreviewWith(fixture.BoundarySource);
        var request = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId,
            fixture.BaselineId, 1, "Confirmed exact preview", fixture.SourceReader.Preview.ContentSha256);
        fixture.SourceReader.BeforeRead = async () =>
        {
            fixture.SourceReader.BeforeRead = null;
            var winner = await fixture.FreezeHandler.HandleAsync(fixture.Context(request), CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            throw new ImmutableContentUnavailableException("The losing preparation saw a later source outage.");
        };

        // Act
        var recovered = await fixture.FreezeHandler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);

        // Assert
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.Equal(2, saved.Owner.Revision);
        Assert.Single(saved.History.Facts.OfType<TypeIBaselineFrozen>());
        Assert.Equal(fixture.BaselineId, saved.Baseline!.TypeIBaselineId);
    }

    [Fact]
    public async Task ShouldFreezeGivenAuthenticatedMcpActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        fixture.SourceReader.Preview = fixture.PreviewWith(fixture.BoundarySource);
        var request = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            fixture.SourceReader.Preview.ContentSha256);

        // Act
        var result = await fixture.FreezeHandler.HandleAsync(fixture.Context(request,
            new McpInvocation("bdgrz.examinations.freeze-type-i-baseline")),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldRejectChangedPreviewGivenFreezeConfirmation()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var shown = fixture.PreviewWith(fixture.BoundarySource);
        var changed = fixture.PreviewWith(fixture.BoundarySource, fixture.ControlSource);
        fixture.SourceReader.Preview = changed;
        var request = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed", shown.ContentSha256);

        // Act
        var result = await fixture.FreezeHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Baseline);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenDifferentProgram()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        fixture.SourceReader.Preview = fixture.PreviewWith(fixture.BoundarySource);
        var request = new FreezeTypeIBaseline(fixture.TenantId, Uuid.CreateVersion4(),
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            fixture.SourceReader.Preview.ContentSha256);

        // Act
        var result = await fixture.FreezeHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectBlockedPreviewGivenFreezeConfirmation()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var preview = TypeIBaselinePreview.Create(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, [fixture.BoundarySource], ["A source family is incomplete"]);
        fixture.SourceReader.Preview = preview;
        var request = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            preview.ContentSha256);

        // Act
        var result = await fixture.FreezeHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Baseline);
    }

    [Fact]
    public async Task ShouldRejectUnauthenticatedActorGivenFreezeConfirmation()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        fixture.SourceReader.Preview = fixture.PreviewWith(fixture.BoundarySource);
        var request = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            fixture.SourceReader.Preview.ContentSha256);
        var actor = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await fixture.FreezeHandler.HandleAsync(fixture.Context(request,
            actor: actor), CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Null(saved.Baseline);
    }

    [Fact]
    public async Task ShouldAllowFreezeRetryAfterSourceChangesGivenSameBaselineIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var original = fixture.PreviewWith(fixture.BoundarySource);
        fixture.SourceReader.Preview = original;
        var request = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            original.ContentSha256);
        var first = await fixture.FreezeHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        fixture.SourceReader.Preview = fixture.PreviewWith(fixture.BoundarySource,
            fixture.ControlSource);

        // Act
        var retry = await fixture.FreezeHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
    }

    [Fact]
    public async Task ShouldAmendAndPreservePriorBaselineGivenChangedSource()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var original = fixture.PreviewWith(fixture.BoundarySource);
        fixture.SourceReader.Preview = original;
        var freeze = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            original.ContentSha256);
        Assert.True((await fixture.FreezeHandler.HandleAsync(fixture.Context(freeze),
            CancellationToken.None)).IsSuccess);
        var amendedPreview = fixture.PreviewWith(fixture.BoundarySource, fixture.ControlSource);
        fixture.SourceReader.Preview = amendedPreview;
        var materialId = Uuid.CreateVersion4();
        var request = new AmendTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.AmendmentId, 2, "Control added to scope",
            [fixture.ControlSource.RecordId], amendedPreview.ContentSha256, [materialId]);

        // Act
        var result = await fixture.AmendHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Collection(saved.Rules.BaselineHistory,
            baseline => Assert.Equal(fixture.BaselineId, baseline.TypeIBaselineId),
            amendment => Assert.Equal(fixture.AmendmentId, amendment.TypeIBaselineId));
        Assert.Equal("amended", saved.Baseline?.Status);
        Assert.Equal(fixture.BaselineId, saved.Baseline?.AmendsBaselineId);
        Assert.Equal([fixture.ControlSource.RecordId], saved.Baseline?.ChangedRecordIds);
        Assert.Equal([materialId], saved.Baseline?.AffectedPackageMaterialIds);
        Assert.Equal("Control added to scope", saved.Baseline?.AmendmentReason);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId),
            saved.Baseline?.ActorMemberId);
    }

    [Fact]
    public async Task ShouldAllowAmendmentRetryAfterSourceChangesGivenSameAmendmentIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var original = fixture.PreviewWith(fixture.BoundarySource);
        fixture.SourceReader.Preview = original;
        var freeze = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            original.ContentSha256);
        Assert.True((await fixture.FreezeHandler.HandleAsync(fixture.Context(freeze),
            CancellationToken.None)).IsSuccess);
        var amendedPreview = fixture.PreviewWith(fixture.BoundarySource, fixture.ControlSource);
        fixture.SourceReader.Preview = amendedPreview;
        var request = new AmendTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.AmendmentId, 2, "Control added to scope",
            [fixture.ControlSource.RecordId], amendedPreview.ContentSha256, []);
        var first = await fixture.AmendHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        fixture.SourceReader.Preview = fixture.PreviewWith(fixture.BoundarySource);

        // Act
        var retry = await fixture.AmendHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
    }

    [Fact]
    public async Task ShouldRejectChangedPreviewGivenAmendment()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var original = fixture.PreviewWith(fixture.BoundarySource);
        fixture.SourceReader.Preview = original;
        var freeze = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            original.ContentSha256);
        Assert.True((await fixture.FreezeHandler.HandleAsync(fixture.Context(freeze),
            CancellationToken.None)).IsSuccess);
        var shown = fixture.PreviewWith(fixture.BoundarySource, fixture.ControlSource);
        var changed = fixture.PreviewWith(fixture.BoundarySource);
        fixture.SourceReader.Preview = changed;
        var request = new AmendTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.AmendmentId, 2, "Control added to scope",
            [fixture.ControlSource.RecordId], shown.ContentSha256, []);

        // Act
        var result = await fixture.AmendHandler.HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single(saved.Rules.BaselineHistory);
    }

    [Fact]
    public async Task ShouldRejectUnauthenticatedActorGivenAmendment()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var original = fixture.PreviewWith(fixture.BoundarySource);
        fixture.SourceReader.Preview = original;
        var freeze = new FreezeTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.BaselineId, 1, "Scope confirmed",
            original.ContentSha256);
        Assert.True((await fixture.FreezeHandler.HandleAsync(fixture.Context(freeze),
            CancellationToken.None)).IsSuccess);
        var amendedPreview = fixture.PreviewWith(fixture.BoundarySource, fixture.ControlSource);
        fixture.SourceReader.Preview = amendedPreview;
        var request = new AmendTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, fixture.AmendmentId, 2, "Control added to scope",
            [fixture.ControlSource.RecordId], amendedPreview.ContentSha256, []);
        var actor = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await fixture.AmendHandler.HandleAsync(fixture.Context(request,
            actor: actor), CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Single(saved.Rules.BaselineHistory);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BoundaryId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid AmendmentId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public InMemoryEventStore Events { get; } = new();
        public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new InMemoryKvClient());
        public ExaminationEngagementReader Examinations => new(Reader, Events, Content);
        public ExaminationEngagementOperations Operations => new(new NativeOperationExecutor(Reader, Executor, Content),
            Examinations, Content, Clock);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public StaticEngagementDirectory Directory { get; } = new();
        public StubPreviewReader SourceReader { get; } = new();
        public TimeProvider Clock { get; } = new FixedClock(Now);
        public TypeIBaselineSourceReference BoundarySource { get; }
        public TypeIBaselineSourceReference ControlSource { get; }
        public FreezeTypeIBaselineHandler FreezeHandler { get; }
        public AmendTypeIBaselineHandler AmendHandler { get; }

        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                new Claim("email", "examiner@example.test")], "BdgrzSession"));

        public Fixture()
        {
            BoundarySource = new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
                Uuid.CreateVersion4(), 1, new string('a', 64));
            ControlSource = new TypeIBaselineSourceReference("control", Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1, new string('b', 64));
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddPortia();
            ExaminationSeeds.Configure(services, Events);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            FreezeHandler = new FreezeTypeIBaselineHandler(Operations, Directory, SourceReader);
            AmendHandler = new AmendTypeIBaselineHandler(Operations, Directory, SourceReader);
        }

        public TypeIBaselinePreview PreviewWith(params TypeIBaselineSourceReference[] sources) =>
            TypeIBaselinePreviewTestFactory.Create(TenantId, ProgramId, ExaminationId, sources);

        public async Task SeedEngagementAsync()
        {
            var engagement = new ExaminationEngagementRules(TenantId, ProgramId, ExaminationId);
            Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i", "Audit Firm",
                new DateOnly(2026, 9, 30), null, ["security"], BoundaryId, []),
                RbacIds.Member(TenantId, UserId), "Examiner", Now));
            await ExaminationSeeds.WriteAsync(Reader, Writer, Events, Content, engagement);
            Directory.Engagement = engagement.Current;
        }

        public Cntryl.Portia.RequestContext<T> Context<T>(T request,
            RequestInvocation? invocation = null, ClaimsPrincipal? actor = null)
            where T : IRequestBase => new(request,
            new RequestDispatchContext(actor ?? Actor, invocation ?? new HttpInvocation("POST",
                "/api/v1/examinations", "/api/v1/examinations", "type-i-baseline-command-test")));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class StubPreviewReader : ITypeIBaselinePreviewReader
    {
        public TypeIBaselinePreview Preview { get; set; } = null!;
        public Func<Task>? BeforeRead { get; set; }

        public async ValueTask<Result<TypeIBaselinePreview>> ReadAsync(
            ExaminationEngagementView engagement, CancellationToken ct = default)
        {
            if (BeforeRead is { } before)
                await before();
            return Result<TypeIBaselinePreview>.Success(Preview);
        }
    }

    sealed class StaticEngagementDirectory : IExaminationEngagementDirectoryReader
    {
        public ExaminationEngagementView? Engagement { get; set; }
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(ProjectionCheckpoint.Start);
        public ValueTask<IReadOnlyList<TypeIBaselineView>> FindBaselinesAsync(Uuid tenantId, Uuid programId, Uuid typeIBaselineId,
            CancellationToken ct = default) => ValueTask.FromResult<IReadOnlyList<TypeIBaselineView>>([]);


        public ValueTask<ExaminationEngagementView?> GetAsync(Uuid tenantId,
            Uuid examinationEngagementId, CancellationToken ct = default) =>
            ValueTask.FromResult(Engagement is
            {
                TenantId: var currentTenant,
                ExaminationEngagementId: var currentId
            } && currentTenant == tenantId && currentId == examinationEngagementId
                ? Engagement
                : null);

        public ValueTask<TypeIBaselineView?> GetBaselineAsync(Uuid tenantId, Uuid examinationEngagementId, Uuid typeIBaselineId,
            CancellationToken ct = default) => ValueTask.FromResult<TypeIBaselineView?>(null);
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
