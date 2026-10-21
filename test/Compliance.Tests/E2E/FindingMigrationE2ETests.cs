using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class FindingMigrationE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldAppendAndRetryLargeFindingImportAsOneFitzBatchGivenLegacyRecord()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        var services = factory.Services;
        var store = services.GetRequiredService<IEventStore>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var businessAt = DateTimeOffset.UtcNow;
        var sourceMetadataAt = businessAt.AddSeconds(1);
        var targetMetadataAt = businessAt.AddSeconds(2);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var source = LargeFinding(tenantId, programId, findingId, actor, businessAt);
        var sourceStream = new EventStreamAddress(tenantId.ToString(), "remediation",
            programId.ToString());
        var sourceEvent = DomainEventSeed.Attach(source, programId, 1,
            occurredOn: sourceMetadataAt);
        await store.AppendAsync(sourceStream, 0, [sourceEvent], timeout.Token);
        var sourceRecord = await ReadSingleAsync(store, sourceStream, timeout.Token);
        var target = new FindingAggregate(tenantId, programId, findingId);
        Assert.Null(target.ImportLegacy(sourceRecord));
        var pending = new AggregateScenario<FindingAggregate>(target).PendingEvents.ToArray();
        var committed = pending.Select((domainEvent, index) => DomainEventSeed.Attach(domainEvent,
            target.Id, (ulong)index + 1, occurredOn: targetMetadataAt)).ToArray();

        // Act
        await store.AppendAsync(target.Stream, 0, committed, timeout.Token);
        var targetRecords = await ReadAllAsync(store, target.Stream, timeout.Token);
        var history = await FindingHistoryFold.ReadAsync(store, tenantId, programId, findingId,
            timeout.Token);
        var hydrated = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new FindingAggregate(tenantId, programId, findingId), timeout.Token);
        var retry = hydrated.ImportLegacy(sourceRecord);
        var retryRecords = await ReadAllAsync(store, target.Stream, timeout.Token);
        var staleBatchFailure = await Record.ExceptionAsync(() => store.AppendAsync(target.Stream,
            0, committed, timeout.Token).AsTask());
        var afterRejectedBatch = await ReadAllAsync(store, target.Stream, timeout.Token);

        // Assert
        Assert.Equal(2, targetRecords.Length);
        Assert.IsType<FindingHistoryEventImported>(targetRecords[0].Event);
        Assert.IsType<FindingRaised>(targetRecords[1].Event);
        Assert.Equal(targetMetadataAt, targetRecords[0].Event.Metadata.OccurredOn);
        Assert.Equal(targetMetadataAt, targetRecords[1].Event.Metadata.OccurredOn);
        Assert.Equal(sourceMetadataAt, history.SourceLineage.Single().SourceOccurredAt);
        Assert.Equal(businessAt, history.History.Single().At);
        Assert.Null(retry);
        Assert.Equal(2, retryRecords.Length);
        Assert.IsType<EventStreamConcurrencyException>(staleBatchFailure);
        Assert.Equal(2, afterRejectedBatch.Length);
    }

    static FindingRaised LargeFinding(Uuid tenantId, Uuid programId, Uuid findingId,
        ActorReference actor, DateTimeOffset at) => new(tenantId, programId, findingId, 1,
        new FindingSource("manual", null, null, new string('s', 6000)), new string('t', 200),
        new string('d', 6000), "medium", new string('a', 2000), Uuid.CreateVersion4(),
        DateOnly.FromDateTime(at.UtcDateTime).AddDays(10),
        Enumerable.Range(0, 40).Select(index => new FindingLink("access_decision",
            new string(index < 5 ? 'é' : 'r', 500))).ToArray(), actor, at);

    static async Task<DomainEventRecord> ReadSingleAsync(IDomainEventReader reader,
        EventStreamAddress stream, CancellationToken ct)
    {
        var records = await ReadAllAsync(reader, stream, ct);
        return Assert.Single(records);
    }

    static async Task<DomainEventRecord[]> ReadAllAsync(IDomainEventReader reader,
        EventStreamAddress stream, CancellationToken ct)
    {
        var records = new List<DomainEventRecord>();
        await foreach (var record in reader.ReadAsync(stream, 0, ct).ConfigureAwait(false))
            records.Add(record);
        return [.. records];
    }
}
