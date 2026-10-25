using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed partial class EvidenceRedactionCompositionTests
{
    internal static async Task<EvidenceRedactionHistorySnapshot> ReadHistoryAsync(IServiceProvider provider, Uuid tenant, Uuid id)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = await services.GetRequiredService<IAggregateReader>().HydrateAsync(new EvidenceRedaction(tenant, id));
        return await EvidenceRedactionHistory.ReadAsync(services.GetRequiredService<IDomainEventReader>(), owner,
            services.GetRequiredService<IImmutableContentStore>(), services.GetRequiredService<ISourceFactResolver>());
    }

    internal static async Task<EvidenceRedactionHistorySnapshot> ReadGivenAsync(IServiceProvider? provider, Uuid tenant, Uuid id,
        params DomainEvent[] facts)
    {
        var owner = new AggregateScenario<EvidenceRedaction>(new EvidenceRedaction(tenant, id)).Given(facts).Aggregate;
        if (provider is null)
            // Explicit occupied v1 replay checks only. No native receipt, source authenticity or current authority is asserted.
            return await EvidenceRedactionHistory.ReadAsync(new GivenRecords(owner.Stream, facts, null), owner);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        return await EvidenceRedactionHistory.ReadAsync(new GivenRecords(owner.Stream, facts,
                services.GetRequiredService<IDomainEventReader>()), owner, services.GetRequiredService<IImmutableContentStore>(),
            services.GetRequiredService<ISourceFactResolver>());
    }

    sealed class GivenRecords(EventStreamAddress stream, IReadOnlyList<DomainEvent> facts, IDomainEventReader? other) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset, CancellationToken ct = default) =>
            address == stream ? Enumerate(fromOffset, ct) : other?.ReadAsync(address, fromOffset, ct) ?? Empty(ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct = default) =>
            other?.ReadAsync(pattern, cursor, ct) ?? Enumerate(0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(ulong from, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            for (var index = from; index < (ulong)facts.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[(int)index], index, EventCursor.Start);
            }
        }
        static async IAsyncEnumerable<DomainEventRecord> Empty([EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            yield break;
        }
    }
}
