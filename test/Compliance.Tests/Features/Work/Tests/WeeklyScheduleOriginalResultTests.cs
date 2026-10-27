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
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WeeklyScheduleOriginalResultTests
{
    static readonly DateOnly WeekStarting = new(2026, 10, 12);

    [Fact]
    public async Task ShouldReturnOriginalPageResultGivenSameScheduleRequestIsRepeated()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        await fixture.SeedRecipientAsync(user, verifiedEmail: true);
        var directory = new Directory(fixture.TenantId, [new TenantMembershipView(user, fixture.TenantId)]);
        var handler = fixture.ScheduleHandler(directory);
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, WeekStarting, "original-page");
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
        var directory = new Directory(fixture.TenantId, []);
        var handler = fixture.ScheduleHandler(directory);
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, WeekStarting, "original-empty-page");
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
            WeeklyWorkDigest.IdFor(fixture.TenantId, RbacIds.Member(fixture.TenantId, user), WeekStarting)));
        Assert.Equal(0UL, owner.Version);
    }

    [Fact]
    public async Task ShouldReplayOriginalScheduleGivenWeekAdmissionWindowHasElapsed()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var handler = fixture.ScheduleHandler(new Directory(fixture.TenantId, []));
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, WeekStarting, "original-previous-week");
        var first = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        fixture.Clock.Set(fixture.Clock.GetUtcNow().AddDays(7));

        // Act
        var retry = await handler.HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.ScheduleContext(request), CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
    }

    sealed class Directory(Uuid tenant, IReadOnlyList<TenantMembershipView> items) : ITenantMembershipDirectoryReader
    {
        public IReadOnlyList<TenantMembershipView> Items { get; set; } = items;
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == tenant.ToString() ? Items.SingleOrDefault(item => item.UserId == userId) : null);
        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            await GetAsync(tenantId, userId, ct) is not null;
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>(tenantId == tenant ? Items.Take(limit).ToArray() : [], null));
    }
}
