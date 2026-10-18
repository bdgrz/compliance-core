using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class ArtifactEngagementHoldHandlerTests : IAsyncDisposable
{
    readonly Uuid _tenantId = Uuid.CreateVersion4();
    readonly Uuid _holdId = Uuid.CreateVersion4();
    readonly Uuid _engagementId = Uuid.CreateVersion4();
    readonly Uuid _userId = Uuid.CreateVersion4();
    readonly InMemoryEventStore _events = new();
    readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    readonly ServiceProvider _services;

    public ArtifactEngagementHoldHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(_events);
        services.AddSingleton(_clock);
        services.AddPortia();
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public async Task ShouldEnforceReportIssuedPlusOneYearBeforeReleasingHoldGivenHandlerWorkflow()
    {
        // Arrange
        await using var scope = _services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var place = new PlaceArtifactEngagementHold(_tenantId, _holdId, _engagementId, null,
            "SOC 2 Type II evidence", "Accepted assurance engagement", "Preserve support through report issuance.");
        var placed = await new PlaceArtifactEngagementHoldHandler(executor, _clock).HandleAsync(
            Context(place), CancellationToken.None);
        Assert.True(placed.IsSuccess, placed.Error?.Message);

        var reportIssuedAt = _clock.GetUtcNow();
        var report = new RecordEngagementReportIssued(_tenantId, _holdId, _engagementId,
            Uuid.CreateVersion4(), reportIssuedAt, "report-2026-001");
        var recorded = await new RecordEngagementReportIssuedHandler(executor, _clock).HandleAsync(
            Context(report), CancellationToken.None);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var hold = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ArtifactEngagementHold(_tenantId, _holdId));
        Assert.Equal(new DateOnly(2027, 10, 1), hold.Current!.ReleaseEligibleOn);

        // Act: release one day before the accepted report-issued-plus-one-year date.
        _clock.UtcNow = new DateTimeOffset(2027, 9, 30, 12, 0, 0, TimeSpan.Zero);
        var early = await new ReleaseArtifactEngagementHoldHandler(executor, _clock).HandleAsync(
            Context(new ReleaseArtifactEngagementHold(_tenantId, _holdId, hold.Revision,
                "Org Admin", "Annual retention period has not elapsed.")), CancellationToken.None);
        var afterEarly = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ArtifactEngagementHold(_tenantId, _holdId));

        _clock.UtcNow = new DateTimeOffset(2027, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var released = await new ReleaseArtifactEngagementHoldHandler(executor, _clock).HandleAsync(
            Context(new ReleaseArtifactEngagementHold(_tenantId, _holdId, afterEarly.Revision,
                "Org Admin", "Annual retention period has elapsed.")), CancellationToken.None);
        var afterRelease = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ArtifactEngagementHold(_tenantId, _holdId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, early.Error?.Kind);
        Assert.Equal("active", afterEarly.Current!.Status);
        Assert.True(released.IsSuccess, released.Error?.Message);
        Assert.Equal("released", afterRelease.Current!.Status);
        Assert.Equal(new DateOnly(2027, 10, 1), afterRelease.Current.ReleaseEligibleOn);
    }

    FixedContext<T> Context<T>(T request) where T : IRequestBase => new(request, Actor(_userId));

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "admin@example.test")], "BdgrzSession"));

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    sealed class FixedContext<T>(T request, ClaimsPrincipal actor) : IRequestContext<T>
        where T : IRequestBase
    {
        public T Request => request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId => RequestId;
        public Uuid? CausationId => null;
        public Uuid CauseId => RequestId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation { get; } = new HttpInvocation("POST", "/retention/holds",
            "/retention/holds", "test");
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
