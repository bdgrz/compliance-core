using System.Collections;
using System.Reflection;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Processes;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WorkDigestHistoryBoundaryTests
{
    [Fact]
    public async Task ShouldRetainBoundedDispatchAuthorityGivenManyCompletedActualMemberWeeks()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var dispatch = new WorkDigestDispatchRules(tenant, member);
        var start = new DateTimeOffset(2025, 1, 6, 9, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < 52; index++)
        {
            var at = start.AddDays(index * 7);
            var week = DateOnly.FromDateTime(at.UtcDateTime);
            Assert.True(dispatch.Schedule(new WorkDigestScheduleWindow(week, "UTC", at), Uuid.CreateVersion4(),
                TimeSpan.FromHours(24), at));
            Assert.True(dispatch.RecordSkipped(week, at.AddMinutes(1), "email_digest_opted_out"));
        }
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        services.AddVerifiedWorkDigestReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await WorkDigestFacts.AppendAsync(provider, new WorkDigestDispatch(tenant, member), dispatch.PendingEvents);

        // Act
        var replayed = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(
            new WorkDigestDispatch(tenant, member));
        var retained = RetainedCollections(replayed);

        // Assert
        Assert.Equal(104UL, replayed.Version);
        Assert.True(retained <= 4, $"The dispatch owner retained {retained} historical weekly entries.");
    }

    [Fact]
    public async Task ShouldRetainBoundedAttemptAuthorityGivenManyAllowedFailedWeeklyAttempts()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var week = new DateOnly(2026, 10, 12);
        var at = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        var digest = new WeeklyWorkDigestRules(tenant, WeeklyWorkDigest.IdFor(tenant, member, week));
        var scheduler = ActorReference.ForSystemProcess(WeeklyWorkDigest.SchedulerProcessId, "Scheduler");
        var worker = ActorReference.ForSystemProcess(WeeklyWorkDigest.DeliveryProcessId, "Delivery worker");
        Assert.True(digest.Schedule(member, week, "actual-member-week", scheduler, at).IsSuccess);
        for (var attempt = 1; attempt <= 128; attempt++)
        {
            var key = WeeklyWorkDigest.DeliveryKeyFor(digest.Id, attempt);
            Assert.True(digest.RequestDelivery(digest.Revision, attempt, key, worker, at.AddMinutes(attempt * 2)).IsSuccess);
            Assert.True(digest.RecordDeliveryResult(digest.Revision, attempt, key, "failed", "provider_unavailable",
                worker, at.AddMinutes(attempt * 2 + 1)).IsSuccess);
        }
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        services.AddVerifiedWorkDigestReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await WorkDigestFacts.WeeklyAsync(provider, tenant, digest.Id, digest);

        // Act
        var replayed = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(
            new WeeklyWorkDigest(tenant, digest.Id));
        var retained = RetainedCollections(replayed);

        // Assert
        Assert.Equal(257, replayed.Revision);
        Assert.Equal("delivery_failed", replayed.Status);
        Assert.True(retained <= 4, $"The weekly owner retained {retained} historical delivery attempts.");
    }

    static int RetainedCollections(Aggregate owner) => owner.GetType().GetFields(BindingFlags.Instance |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Select(field => field.GetValue(owner)).OfType<IEnumerable>()
        .Where(value => value is not string).Sum(value => value.Cast<object>().Count());
}
