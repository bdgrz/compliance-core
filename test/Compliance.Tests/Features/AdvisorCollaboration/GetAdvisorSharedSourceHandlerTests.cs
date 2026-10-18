using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class GetAdvisorSharedSourceHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReturnSelectedSourceGivenUnexpiredReadyHandoff()
    {
        // Arrange
        await using var fixture = new Fixture();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 2, 4);
        var handoffId = await fixture.SeedHandoffAsync(selected, "ready_for_review",
            Now.AddMinutes(-1), new DateOnly(2026, 12, 31));
        var sources = new FixedSharedSourceReader();
        var request = new GetAdvisorSharedSource(fixture.TenantId, fixture.ProgramId,
            handoffId, selected.SourceFamily, selected.RecordId);

        // Act
        var result = await new GetAdvisorSharedSourceHandler(fixture.Reader, sources,
                new FixedTimeProvider(Now))
            .HandleAsync(new RequestContext<GetAdvisorSharedSource>(request, RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(selected, result.Value.SelectedSource);
        Assert.Equal(1, sources.Calls);
    }

    [Fact]
    public async Task ShouldNotReadSourceOutsideHandoffSelectionGivenKnownRecordIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 2, 4);
        var handoffId = await fixture.SeedHandoffAsync(selected, "ready_for_review",
            Now.AddMinutes(-1), new DateOnly(2026, 12, 31));
        var sources = new FixedSharedSourceReader();
        var request = new GetAdvisorSharedSource(fixture.TenantId, fixture.ProgramId,
            handoffId, selected.SourceFamily, Uuid.CreateVersion4());

        // Act
        var result = await new GetAdvisorSharedSourceHandler(fixture.Reader, sources,
                new FixedTimeProvider(Now))
            .HandleAsync(new RequestContext<GetAdvisorSharedSource>(request, RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(0, sources.Calls);
    }

    [Fact]
    public async Task ShouldNotReadSourceGivenExpiredHandoff()
    {
        // Arrange
        await using var fixture = new Fixture();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 2, 4);
        var handoffId = await fixture.SeedHandoffAsync(selected, "ready_for_review",
            Now.AddDays(-2), new DateOnly(2026, 10, 17));
        var sources = new FixedSharedSourceReader();
        var request = new GetAdvisorSharedSource(fixture.TenantId, fixture.ProgramId,
            handoffId, selected.SourceFamily, selected.RecordId);

        // Act
        var result = await new GetAdvisorSharedSourceHandler(fixture.Reader, sources,
                new FixedTimeProvider(Now))
            .HandleAsync(new RequestContext<GetAdvisorSharedSource>(request, RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(0, sources.Calls);
    }

    [Fact]
    public async Task ShouldNotReturnSourceGivenHandoffClosedDuringSourceRead()
    {
        // Arrange
        await using var fixture = new Fixture();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 2, 4);
        var handoffId = await fixture.SeedHandoffAsync(selected, "ready_for_review",
            Now.AddMinutes(-1), new DateOnly(2026, 12, 31));
        var sources = new FixedSharedSourceReader(_ => new ValueTask(
            fixture.CloseHandoffAsync(handoffId, Now.AddSeconds(1))));
        var request = new GetAdvisorSharedSource(fixture.TenantId, fixture.ProgramId,
            handoffId, selected.SourceFamily, selected.RecordId);

        // Act
        var result = await new GetAdvisorSharedSourceHandler(fixture.Reader, sources,
                new FixedTimeProvider(Now))
            .HandleAsync(new RequestContext<GetAdvisorSharedSource>(request, RequestActor.System),
                CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(1, sources.Calls);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid MemberId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        }

        public async Task<Uuid> SeedHandoffAsync(AdvisorRecordVersion selected, string status,
            DateTimeOffset createdAt, DateOnly expiresOn)
        {
            var handoffId = Uuid.CreateVersion4();
            var handoff = new AdvisorHandoff(TenantId, ProgramId, handoffId);
            var lead = ActorReference.ForMember(MemberId, "Lead");
            Assert.True(handoff.Create("Review", [selected], expiresOn, lead, createdAt).IsSuccess);
            if (status == "ready_for_review")
                Assert.True(handoff.MarkReadyForReview(1, lead, createdAt.AddMinutes(1)).IsSuccess);
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
            return handoffId;
        }

        public async Task CloseHandoffAsync(Uuid handoffId, DateTimeOffset closedAt)
        {
            var handoff = await Reader.HydrateAsync(new AdvisorHandoff(TenantId, ProgramId,
                handoffId));
            Assert.True(handoff.Close("Review complete", ActorReference.ForMember(MemberId,
                "Lead"), closedAt).IsSuccess);
            await Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedSharedSourceReader(Func<CancellationToken, ValueTask>? onRead = null)
        : IAdvisorSharedSourceReader
    {
        public int Calls { get; private set; }

        public async ValueTask<Result<AdvisorSharedSourceView>> GetAsync(Uuid tenantId, Uuid programId,
            Uuid handoffId, AdvisorRecordVersion selectedSource, CancellationToken ct = default)
        {
            Calls++;
            if (onRead is not null)
                await onRead(ct);
            return Result<AdvisorSharedSourceView>.Success(new AdvisorSharedSourceView(tenantId,
                programId, handoffId, selectedSource, null, null, null, null, null, null, null));
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
