using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Verifies immutable history at a hydrated compact owner's exact physical and native cut.</summary>
public static class NativeOwnerHistory
{
    /// <summary>Reads only the committed cut captured by the owner, including audits in physical offset accounting.</summary>
    public static async Task<IReadOnlyList<TFact>> ReadAsync<TFact>(IDomainEventReader events, Aggregate owner,
        CancellationToken ct = default) where TFact : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(owner);
        var through = owner.CommittedStreamPosition;
        if (through == 0)
            return [];
        var facts = new List<TFact>();
        var eventIds = new HashSet<Uuid>();
        ulong offset = 0;
        ulong version = 0;
        await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
        {
            if (offset == through)
                break;
            var fact = record.Event;
            var metadata = fact.Metadata;
            if (record.Stream != owner.Stream || record.ResourceOffset != offset ||
                metadata.EventId == Uuid.Empty || metadata.AggregateId != owner.Id ||
                !eventIds.Add(metadata.EventId))
                throw new ImmutableContentIntegrityException("The native history has another source identity or physical prefix.");
            offset++;
            if (metadata.IsAudit)
                continue;
            if (fact is not TFact typed || metadata.AggregateVersion != ++version)
                throw new ImmutableContentIntegrityException("The native history has another fact type or logical version.");
            facts.Add(typed);
            if (offset == through)
                break;
        }
        if (offset != through || version != owner.Version)
            throw new ImmutableContentUnavailableException("The exact hydrated native history cut is not visible yet.");
        return facts;
    }

    /// <summary>Requires a root fact to retain its exact first, affected and revision owner and original revision.</summary>
    public static bool IsOwnRoot(Aggregate owner, OperationProvenance operation, string commandKind,
        ulong? expectedRevision) => operation.LogicalId != Uuid.Empty &&
        operation.RootOperationId == operation.LogicalId && operation.CausationEventId is null &&
        operation.CommandKind == commandKind && operation.Owner == owner.Stream &&
        (operation.FirstOwner ?? operation.Owner) == owner.Stream && operation.AffectedOwner == owner.Stream &&
        operation.ExpectedRevisionOwner == owner.Stream && operation.OriginalExpectedRevision == expectedRevision &&
        operation.Episode == RootOperation.InitialEpisode && operation.AuthoredIntentSha256.Length == 64 &&
        operation.AuthoredIntentSha256.All(Uri.IsHexDigit) && operation.AcceptedAt != default;
}
