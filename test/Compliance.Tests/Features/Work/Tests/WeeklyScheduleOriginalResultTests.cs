using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WeeklyScheduleOriginalResultTests
{
    static DateOnly CurrentWeekStarting(WeeklyWorkDigestCommandHandlerTests.Fixture fixture)
    {
        var today = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime);
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
    }

    [Fact]
    public async Task ShouldReturnOriginalPageResultGivenSameScheduleRequestIsRepeated()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var weekStarting = CurrentWeekStarting(fixture);
        var user = Uuid.CreateVersion4();
        await fixture.SeedRecipientAsync(user, verifiedEmail: true);
        var directory = new Directory(fixture.TenantId, [new TenantMembershipView(user, fixture.TenantId)]);
        var handler = fixture.ScheduleHandler(directory);
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, weekStarting, "original-page");
        var first = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(1, first.Value.DigestsScheduled);

        // Act
        var retry = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
    }

    [Fact]
    public async Task ShouldRetainEmptyOriginalPageGivenMembershipAppearsAfterScheduleAcceptance()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var weekStarting = CurrentWeekStarting(fixture);
        var directory = new Directory(fixture.TenantId, []);
        var handler = fixture.ScheduleHandler(directory);
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, weekStarting, "original-empty-page");
        var first = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(new WorkDigestSchedulePageView(0, 0, 0, null), first.Value);
        var user = Uuid.CreateVersion4();
        await fixture.SeedRecipientAsync(user, verifiedEmail: true);
        directory.Items = [new TenantMembershipView(user, fixture.TenantId)];

        // Act
        var retry = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        var owner = await fixture.Reader.HydrateAsync(new WeeklyWorkDigest(fixture.TenantId,
            WeeklyWorkDigest.IdFor(fixture.TenantId, RbacIds.Member(fixture.TenantId, user), weekStarting)));
        Assert.Equal(0UL, owner.Version);
    }

    [Fact]
    public async Task ShouldReplayOriginalScheduleGivenWeekAdmissionWindowHasElapsed()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var weekStarting = CurrentWeekStarting(fixture);
        var handler = fixture.ScheduleHandler(new Directory(fixture.TenantId, []));
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, weekStarting, "original-previous-week");
        var first = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        fixture.Clock.Set(fixture.Clock.GetUtcNow().AddDays(7));

        // Act
        var retry = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
    }

    [Fact]
    public async Task ShouldReplayOriginalPageGivenRecreatedHandler()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        await fixture.SeedRecipientAsync(user, verifiedEmail: true);
        var directory = new Directory(fixture.TenantId, [new TenantMembershipView(user, fixture.TenantId)]);
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, CurrentWeekStarting(fixture), "recreated-page");
        var first = await fixture.ScheduleHandler(directory).HandleAsync(
            WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var replay = await fixture.ScheduleHandler(new Directory(fixture.TenantId, [])).HandleAsync(
            WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
    }

    [Fact]
    public async Task ShouldRefuseChangedLimitGivenAcceptedPageIdentity()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var handler = fixture.ScheduleHandler(new Directory(fixture.TenantId, []));
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, CurrentWeekStarting(fixture), "limit-page");
        Assert.True((await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request),
            CancellationToken.None)).IsSuccess);

        // Act
        var changed = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(
            request with { Limit = request.Limit + 1 }), CancellationToken.None);

        // Assert
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRecoverOriginalScheduledCountGivenLostAppendResponse()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        await fixture.SeedRecipientAsync(user, verifiedEmail: true);
        var directory = new Directory(fixture.TenantId, [new TenantMembershipView(user, fixture.TenantId)]);
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, CurrentWeekStarting(fixture), "lost-append-page");
        var interrupted = fixture.ScheduleHandler(directory, new LostResponseExecutor(fixture.Executor));
        await Assert.ThrowsAsync<TimeoutException>(async () => await interrupted.HandleAsync(
            WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None));

        // Act
        var recovered = await fixture.ScheduleHandler(new Directory(fixture.TenantId, [])).HandleAsync(
            WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);

        // Assert
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.Equal(new WorkDigestSchedulePageView(1, 1, 0, null), recovered.Value);
    }

    [Fact]
    public async Task ShouldRefuseReplayGivenUntrustedScheduler()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var handler = fixture.ScheduleHandler(new Directory(fixture.TenantId, []));
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, CurrentWeekStarting(fixture), "authorized-page");
        Assert.True((await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request),
            CancellationToken.None)).IsSuccess);

        // Act
        var replay = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(
            request, "scheduler:Other"), CancellationToken.None);

        // Assert
        Assert.False(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, replay.Error!.Kind);
    }

    [Fact]
    public async Task ShouldScheduleMemberOnceGivenDuplicateDirectoryRows()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        await fixture.SeedRecipientAsync(user, verifiedEmail: true);
        var membership = new TenantMembershipView(user, fixture.TenantId);
        var handler = fixture.ScheduleHandler(new Directory(fixture.TenantId, [membership, membership]));
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, CurrentWeekStarting(fixture), "duplicate-page");

        // Act
        var result = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new WorkDigestSchedulePageView(2, 1, 1, null), result.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldConvergeOnOneScheduleGivenCompetingPages(bool differentPage)
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        await fixture.SeedRecipientAsync(user, verifiedEmail: true);
        var directory = new RacingDirectory(new Directory(fixture.TenantId,
            [new TenantMembershipView(user, fixture.TenantId)]));
        var first = new ScheduleWeeklyWorkDigest(fixture.TenantId, CurrentWeekStarting(fixture), "racing-page");
        var second = differentPage ? first with { Cursor = "next-page" } : first;

        // Act
        var results = await Task.WhenAll(fixture.ScheduleHandler(directory).HandleAsync(
                WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(first), CancellationToken.None).AsTask(),
            fixture.ScheduleHandler(directory).HandleAsync(
                WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(second), CancellationToken.None).AsTask());

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        if (differentPage)
            Assert.Equal(1, results.Sum(result => result.Value.DigestsScheduled));
        else
        {
            Assert.Equal(results[0].Value, results[1].Value);
            Assert.Equal(1, results[0].Value.DigestsScheduled);
        }
        var owner = await fixture.Reader.HydrateAsync(new WeeklyWorkDigest(fixture.TenantId,
            WeeklyWorkDigest.IdFor(fixture.TenantId, RbacIds.Member(fixture.TenantId, user), first.WeekStarting)));
        Assert.Equal(1UL, owner.Version);
    }

    [Fact]
    public async Task ShouldRefuseBeforeSchedulingGivenDirectoryExceedsRequestedBound()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        await fixture.SeedRecipientAsync(user, verifiedEmail: true);
        var membership = new TenantMembershipView(user, fixture.TenantId);
        var directory = new Directory(fixture.TenantId, [membership, membership]) { RespectLimit = false };
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, CurrentWeekStarting(fixture), "oversized-page", 1);

        // Act
        var result = await fixture.ScheduleHandler(directory).HandleAsync(
            WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        var owner = await fixture.Reader.HydrateAsync(new WeeklyWorkDigest(fixture.TenantId,
            WeeklyWorkDigest.IdFor(fixture.TenantId, RbacIds.Member(fixture.TenantId, user), request.WeekStarting)));
        Assert.Equal(0UL, owner.Version);
    }

    sealed class RacingDirectory(Directory inner) : ITenantMembershipDirectoryReader
    {
        readonly TaskCompletionSource<bool> _bothReading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _readers;

        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            inner.GetAsync(tenantId, userId, ct);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            inner.IsMemberAsync(tenantId, userId, ct);
        public async ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _readers) == 2)
                _bothReading.TrySetResult(true);
            await _bothReading.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            return await inner.ListAsync(tenantId, limit, cursor, ct);
        }
    }

    sealed class Directory(Uuid tenant, IReadOnlyList<TenantMembershipView> items) : ITenantMembershipDirectoryReader
    {
        public IReadOnlyList<TenantMembershipView> Items { get; set; } = items;
        public bool RespectLimit { get; init; } = true;
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == tenant.ToString() ? Items.SingleOrDefault(item => item.UserId == userId) : null);
        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            await GetAsync(tenantId, userId, ct) is not null;
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>(tenantId == tenant
                ? RespectLimit ? Items.Take(limit).ToArray() : Items : [], null));
    }
}
