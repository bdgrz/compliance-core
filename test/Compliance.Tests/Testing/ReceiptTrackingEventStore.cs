using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit test commit authority: records its controlled clock only after a successful append, independently of event/operation timestamps.</summary>
sealed class ReceiptTrackingEventStore(IEventStore inner, TimeProvider clock) : IEventStore, INativeEventReceiptReader
{
    readonly object _gate = new();
    readonly Dictionary<(EventStreamAddress Stream, ulong Offset), (Uuid Event, ulong Version, string Kind, string Sha256, DateTimeOffset At)> _receipts = [];
    public TimeProvider Clock { get; set; } = clock;
    public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset, CancellationToken ct) => inner.ReadAsync(stream, offset, ct);
    public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct) => inner.ReadAsync(pattern, cursor, ct);
    public async ValueTask AppendAsync(EventStreamAddress stream, ulong expected, IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
    {
        await inner.AppendAsync(stream, expected, events, ct).ConfigureAwait(false);
        var at = DateTimeOffset.FromUnixTimeMilliseconds(Clock.GetUtcNow().ToUnixTimeMilliseconds());
        lock (_gate)
            for (var index = 0; index < events.Count; index++)
            {
                var value = events[index];
                _receipts.Add((stream, checked(expected + (ulong)index)), (value.Metadata.EventId, value.Metadata.AggregateVersion,
                    EventKind.Of(value), ContentDigest.Sha256(value, value.GetType()), at));
            }
    }
    public ValueTask<DateTimeOffset> RecordedAtAsync(DomainEventRecord source, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_receipts.TryGetValue((source.Stream, source.ResourceOffset), out var value))
                throw new ImmutableContentUnavailableException("The test commit receipt is absent.");
            if (source.Event.Metadata.IsAudit || source.Event.Metadata.EventId != value.Event || source.Event.Metadata.AggregateVersion != value.Version ||
                EventKind.Of(source.Event) != value.Kind || ContentDigest.Sha256(source.Event, source.Event.GetType()) != value.Sha256)
                throw new ImmutableContentIntegrityException("The test source differs from its exact recorded commit receipt.");
            return ValueTask.FromResult(value.At);
        }
    }
}
