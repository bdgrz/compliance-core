using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Hosting.Persistence;

/// <summary>Preflights every attributed envelope before the inner store can open an append session.</summary>
sealed class PersistenceEventStore(IEventStore inner, IDomainEventSerializer serializer)
    : IEventStore, IDomainEventNotifier
{
    public ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
        IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(events);
        ct.ThrowIfCancellationRequested();
        DomainEvent[] pending = [.. events];
        var snapshots = new DomainEvent[pending.Length];
        for (var index = 0; index < pending.Length; index++)
        {
            ct.ThrowIfCancellationRequested();
            var envelope = serializer.Serialize(pending[index]);
            FitzFrameLimits.CheckEvent(stream, envelope.Length);
            if ((pending[index].GetType().Namespace == typeof(EditionLabelReserved).Namespace &&
                 pending[index] is not CriteriaTextOverlayEntryRevised) ||
                pending[index] is PlatformOperatorAccessGranted or PlatformOperatorAccessRevoked or
                    PlatformOperatorBootstrapAccessGranted or PlatformOperatorBootstrapCompleted or PlatformOperatorBootstrapGrantAndMarkAccepted or
                    PlatformOperatorPrimaryMarkAccepted or
                    PlatformOperatorPrimaryMarkFenced or PlatformOperatorPrimaryUnmarked or
                    PrimaryPlatformOperatorSeatReserved or PrimaryPlatformOperatorAppointed or
                    PrimaryPlatformOperatorSeatRefused or PrimaryPlatformOperatorSeatReleased or
                    PrimaryPlatformOperatorSeatVacated or AccessPolicyActivated)
                FitzFrameLimits.CheckSmallEvent(stream, envelope.Length);
            var snapshot = serializer.Deserialize(envelope);
            if (!serializer.Serialize(snapshot).Span.SequenceEqual(envelope.Span))
                throw new InvalidOperationException("A persisted event must preserve its canonical envelope through serialization.");
            snapshots[index] = snapshot;
        }
        return inner.AppendAsync(stream, expectedStreamPosition, snapshots, ct);
    }

    public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
        CancellationToken ct) => inner.ReadAsync(stream, fromOffset, ct);

    public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
        CancellationToken ct) => inner.ReadAsync(pattern, cursor, ct);

    public ValueTask<IDomainEventSubscription> SubscribeAsync(EventStreamPattern pattern,
        CancellationToken ct = default) => inner is IDomainEventNotifier notifier
        ? notifier.SubscribeAsync(pattern, ct)
        : throw new NotSupportedException("The underlying event store does not support notifications.");
}
