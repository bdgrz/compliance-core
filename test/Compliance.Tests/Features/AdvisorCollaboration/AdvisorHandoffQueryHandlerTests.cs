using System.Security.Claims;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorHandoffQueryHandlerTests
{
    static readonly DateTimeOffset CreatedAt = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReturnSelectedExactVersionsAndLifecycleGivenScopedHandoff()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = Uuid.CreateVersion4();
        var record = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var createdBy = ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId),
            "Compliance lead");
        var handoff = new AdvisorHandoff(fixture.TenantId, fixture.ProgramId, handoffId);
        Assert.True(handoff.Create("Type I readiness review", [record], new DateOnly(2026, 12, 31),
            createdBy, CreatedAt).IsSuccess);
        Assert.True(handoff.MarkReadyForReview(1, createdBy, CreatedAt.AddMinutes(1)).IsSuccess);
        await fixture.Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));

        // Act
        var result = await new GetAdvisorRequestHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetAdvisorRequest>(new GetAdvisorRequest(fixture.TenantId,
                fixture.ProgramId, handoffId), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.TenantId, result.Value.TenantId);
        Assert.Equal(fixture.ProgramId, result.Value.ProgramId);
        Assert.Equal(handoffId, result.Value.AdvisorRequestId);
        Assert.Equal("Type I readiness review", result.Value.Purpose);
        Assert.Equal(new DateOnly(2026, 12, 31), result.Value.ExpiresOn);
        Assert.Equal("ready_for_review", result.Value.Status);
        Assert.Equal(2, result.Value.Revision);
        Assert.Equal(CreatedAt, result.Value.CreatedAt);
        Assert.Equal(CreatedAt.AddMinutes(1), result.Value.UpdatedAt);
        Assert.Equal(createdBy, result.Value.CreatedBy);
        Assert.Equal([record], result.Value.RecordVersions);
    }

    [Fact]
    public async Task ShouldNotDiscloseHandoffGivenDifferentProgramScope()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handoffId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId),
            "Compliance lead");
        var handoff = new AdvisorHandoff(fixture.TenantId, fixture.ProgramId, handoffId);
        Assert.True(handoff.Create("Type I readiness review",
            [new(Uuid.CreateVersion4(), Uuid.CreateVersion4())], new DateOnly(2026, 12, 31),
            actor, CreatedAt).IsSuccess);
        await fixture.Writer.SaveAsync(handoff, new RequestDispatchContext(RequestActor.System));

        // Act
        var result = await new GetAdvisorRequestHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetAdvisorRequest>(new GetAdvisorRequest(fixture.TenantId,
                Uuid.CreateVersion4(), handoffId), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenMissingHandoff()
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        var result = await new GetAdvisorRequestHandler(fixture.Reader).HandleAsync(
            new RequestContext<GetAdvisorRequest>(new GetAdvisorRequest(fixture.TenantId,
                fixture.ProgramId, Uuid.CreateVersion4()), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
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

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
