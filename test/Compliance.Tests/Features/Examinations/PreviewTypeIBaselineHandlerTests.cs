using System.Security.Claims;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class PreviewTypeIBaselineHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReturnSourceBackedPreviewAndEngagementRevisionGivenExistingEngagement()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new PreviewTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId);

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.TenantId, result.Value.TenantId);
        Assert.Equal(fixture.ProgramId, result.Value.ProgramId);
        Assert.Equal(fixture.ExaminationId, result.Value.ExaminationEngagementId);
        Assert.Equal(1, result.Value.EngagementRevision);
        Assert.Equal(new DateOnly(2026, 9, 30), result.Value.AsOf);
        Assert.Equal(fixture.Preview.ContentSha256, result.Value.ContentSha256);
        Assert.Equal(fixture.Preview.Sources, result.Value.Sources);
        Assert.Equal(fixture.Preview.Blockers, result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenEngagementFromDifferentProgram()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new PreviewTypeIBaseline(fixture.TenantId, Uuid.CreateVersion4(),
            fixture.ExaminationId);

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectPreviewFromDifferentEngagementGivenSourceReader()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        fixture.SourceReader.Preview = TypeIBaselinePreview.Create(fixture.TenantId,
            fixture.ProgramId, Uuid.CreateVersion4(), fixture.Preview.Sources,
            fixture.Preview.Blockers, fixture.Preview.ScopeDifferences);
        var request = new PreviewTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId);

        // Act
        var result = await fixture.Handler.HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BoundaryId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader AggregateReader { get; }
        public IAggregateWriter AggregateWriter { get; }
        public TypeIBaselinePreview Preview { get; }
        public StaticEngagementDirectory Directory { get; }
        public StaticPreviewReader SourceReader { get; }
        public PreviewTypeIBaselineHandler Handler { get; }

        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                new Claim("email", "lead@example.test")], "BdgrzSession"));

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            AggregateReader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            AggregateWriter = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Directory = new StaticEngagementDirectory();
            var boundary = new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
                Uuid.CreateVersion4(), 1, new string('a', 64));
            Preview = TypeIBaselinePreview.Create(TenantId, ProgramId, ExaminationId, [boundary],
                ["The workforce source family has not been captured."]);
            SourceReader = new StaticPreviewReader(Preview);
            Handler = new PreviewTypeIBaselineHandler(AggregateReader, Directory, SourceReader);
        }

        public async Task SeedEngagementAsync()
        {
            var engagement = new ExaminationEngagement(TenantId, ProgramId, ExaminationId);
            Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i", "Audit Firm",
                new DateOnly(2026, 9, 30), null, ["security"], BoundaryId, []),
                RbacIds.Member(TenantId, UserId), "Lead", Now));
            await AggregateWriter.SaveAsync(engagement,
                new RequestDispatchContext(RequestActor.System));
            Directory.Engagement = engagement.Current;
        }

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor, new HttpInvocation("GET",
                "/api/v1/type-i-baseline-preview", "/api/v1/type-i-baseline-preview",
                "type-i-baseline-preview-test")));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
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
            } &&
                currentTenant == tenantId && currentId == examinationEngagementId
                    ? Engagement
                    : null);

        public ValueTask<TypeIBaselineView?> GetBaselineAsync(Uuid tenantId, Uuid examinationEngagementId,
            Uuid typeIBaselineId, CancellationToken ct = default) =>
            ValueTask.FromResult<TypeIBaselineView?>(null);
    }

    sealed class StaticPreviewReader(TypeIBaselinePreview preview) : ITypeIBaselinePreviewReader
    {
        public TypeIBaselinePreview Preview { get; set; } = preview;

        public ValueTask<Result<TypeIBaselinePreview>> ReadAsync(
            ExaminationEngagementView engagement, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<TypeIBaselinePreview>.Success(Preview));
    }
}
