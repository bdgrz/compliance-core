using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Features.Incidents;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Incidents;

public sealed class IncidentCreationRetryTests
{
    [Fact]
    public async Task ShouldReplayCreationWithoutNewFactGivenClockMovesBeforeExactRetry()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var incident = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var clock = new ClockStub();
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var incidents = new ComplianceIncidentReader(reader, events, content);
        var handler = new RecordComplianceIncidentHandler(new IncidentOperations(new NativeOperationExecutor(reader, executor, content), incidents, content, clock));
        var body = new ComplianceIncidentContent(null, null, null, null, "Credential exposure", "Complete original authored incident.", "security", "high",
            clock.Now.AddHours(-2), clock.Now.AddHours(-1), RbacIds.Member(tenant, user), true, false, [Uuid.CreateVersion4()], "confidential", []);
        var request = new RecordComplianceIncident(tenant, program, incident, body);
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", user.ToString()), new Claim("name", "Reporter")], "UnitTest"));
        Assert.True((await handler.HandleAsync(new RequestContext<RecordComplianceIncident>(request, actor), CancellationToken.None)).IsSuccess);
        clock.Now = clock.Now.AddHours(1);

        // Act
        var retry = await handler.HandleAsync(new RequestContext<RecordComplianceIncident>(request, actor), CancellationToken.None);
        var facts = new List<DomainEvent>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), "compliance-incidents", incident.ToString()), EventCursor.Start, CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                facts.Add(record.Event);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Single(facts);
    }
    sealed class ClockStub : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
