using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

public sealed class ControlEvaluationReplayCutTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectDifferentRawPrefixGivenActualHydratedEvaluationCut(string fault)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var state = await SeedAsync(fixture);
        var facts = Alternate(state.Facts, fault);

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await ControlEvaluationHistory.ReadAsync(
            new Records(state.Owner.Stream, facts), state.Owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(error);
        else
            Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3, state.Owner.Revision);
    }

    [Fact]
    public async Task ShouldRejectDifferentPreparedPrefixGivenSameVersionTailAndLatestFact()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var state = await SeedAsync(fixture);
        var store = new InMemoryEventStore();
        await store.AppendAsync(state.Owner.Stream, 0, Alternate(state.Facts, "event_id"));
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddPortia();
        services.AddVerifiedEvaluationReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var alternateReader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var alternate = await alternateReader.HydrateAsync(new ControlEvaluation(fixture.TenantId, state.Owner.Id));
        await using var originalScope = fixture.Provider.CreateAsyncScope();
        var original = originalScope.ServiceProvider;
        var reader = original.GetRequiredService<IAggregateReader>();
        var native = new NativeOperationExecutor(reader, scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
            fixture.Content);
        var handler = new RecordControlEvaluationStepHandler(native, original.GetRequiredService<ControlEvaluationReader>(),
            fixture.Content, TimeProvider.System);
        var command = ControlEvaluationTests.Record(fixture, state.View, 0);
        var context = new InventoryRequestContext<RecordControlEvaluationStep>(command,
            ProgramManagementServices.Actor(fixture.OwnerUserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var after = await alternateReader.HydrateAsync(new ControlEvaluation(fixture.TenantId, state.Owner.Id));

        // Assert
        Assert.Equal(state.Owner.Version, alternate.Version);
        Assert.Equal(state.Owner.CommittedStreamPosition, alternate.CommittedStreamPosition);
        Assert.Equal(state.Owner.Latest, alternate.Latest);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(3, after.Revision);
    }

    [Fact]
    public async Task ShouldPreserveCapturedEarlierCutGivenLegitimateLaterStepCorrection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var state = await SeedAsync(fixture);
        await fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Record(fixture, state.View, 0));

        // Act
        var history = await ControlEvaluationHistory.ReadAsync(fixture.Provider.GetRequiredService<IDomainEventReader>(),
            state.Owner);

        // Assert
        Assert.Equal(3, history.Count);
        Assert.Equal(3, state.Owner.Revision);
    }

    static async Task<Seed> SeedAsync(OperationsFixture fixture)
    {
        var current = await ControlEvaluationTests.StartAsync(fixture);
        current = await fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Record(fixture, current, 0));
        current = await fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Record(fixture, current, 1));
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlEvaluation(fixture.TenantId, current.EvaluationId));
        var facts = new List<DomainEvent>();
        await foreach (var record in fixture.Provider.GetRequiredService<IDomainEventReader>().ReadAsync(owner.Stream, 0,
                           CancellationToken.None))
            facts.Add(record.Event);
        return new Seed(current, owner, facts.ToArray());
    }

    static DomainEvent[] Alternate(DomainEvent[] originalFacts, string fault)
    {
        if (fault == "truncate")
            return originalFacts[..^1];
        var facts = originalFacts.ToArray();
        var original = Assert.IsType<ControlEvaluationStepRecorded>(facts[1]);
        var changed = fault == "payload" ? original with { StepId = Uuid.CreateVersion4() } : original with { };
        changed.AttachMetadata(fault == "event_id"
            ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
        facts[1] = changed;
        return facts;
    }

    sealed record Seed(ControlEvaluationView View, ControlEvaluation Owner, DomainEvent[] Facts);

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
