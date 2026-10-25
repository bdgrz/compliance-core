using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ProductClientServiceMetadataCommitmentTests
{
    [Theory]
    [InlineData("correlation", false)]
    [InlineData("causation", false)]
    [InlineData("execution", false)]
    [InlineData("actor", false)]
    [InlineData("correlation", true)]
    [InlineData("causation", true)]
    [InlineData("execution", true)]
    [InlineData("actor", true)]
    public async Task ShouldRejectDifferentUnselectedSavedMetadataGivenCapturedServicePrefix(string field, bool execute)
    {
        // Arrange
        await using var original = new ProductClientServiceFixture();
        var tenant = Uuid.CreateVersion4();
        var service = Uuid.CreateVersion4();
        var rules = new ProductClientServiceRules(tenant, service);
        var member = Uuid.CreateVersion4();
        var at = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        Assert.True(rules.Create(Uuid.CreateVersion4(), "Payroll", "Process payroll", "Operations", member, "Author", at).IsSuccess);
        Assert.Null(rules.Revise(1, "Second", "Purpose", "Owner", member, "Author", at.AddMinutes(1)));
        Assert.Null(rules.Revise(2, "Third", "Purpose", "Owner", member, "Author", at.AddMinutes(2)));
        await original.PublishAsync(rules);
        var history = await original.Services.HistoryAsync(tenant, service);
        var facts = history.Facts.ToArray();
        var second = Assert.IsType<ClientServiceRevised>(facts[1]);
        var changed = second with { };
        changed.AttachMetadata(field switch
        {
            "correlation" => second.Metadata with { CorrelationId = Uuid.CreateVersion4() },
            "causation" => second.Metadata with { CausationId = Uuid.CreateVersion4() },
            "execution" => second.Metadata with { ExecutionId = Uuid.CreateVersion4() },
            _ => second.Metadata with { Actor = new ActorAttribution("another-worker", "bdgrz.system") },
        });
        Assert.Equal(second.Metadata.EventId, changed.Metadata.EventId);
        Assert.Equal(second.Metadata.AggregateVersion, changed.Metadata.AggregateVersion);
        facts[1] = changed;
        await using var fixture = new ProductClientServiceFixture(original.Events, new Records(history.Owner.Stream, facts));
        var reached = false;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (execute)
                _ = await fixture.Executor.ExecuteAsync(new ClientService(tenant, service), _ =>
                {
                    reached = true;
                    return AggregateOutcome.Discard(Result.Success);
                }, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            else
                _ = await fixture.Reader.HydrateAsync(new ClientService(tenant, service));
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(reached);
    }

    sealed class Records(EventStreamAddress stream, IReadOnlyList<DomainEvent> facts) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress selected, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (selected != stream)
                yield break;
            for (var index = checked((int)fromOffset); index < facts.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], (ulong)index, EventCursor.Start);
            }
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var record in ReadAsync(stream, 0, ct).ConfigureAwait(false))
                yield return record;
        }
    }
}
