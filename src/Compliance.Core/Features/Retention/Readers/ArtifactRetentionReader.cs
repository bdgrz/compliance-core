using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Events;
using Bdgrz.Compliance.Features.Retention.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Readers;

/// <summary>Discovers holds from native placement facts and composes each independent authoritative owner.</summary>
public sealed class ArtifactRetentionReader(IAggregateReader aggregates, IDomainEventReader events)
{
    public async ValueTask<Result<ArtifactRetentionSnapshot>> ReadAsync(ArtifactRetentionSourceSnapshot source,
        CancellationToken ct = default)
    {
        var legacy = await ArtifactRetentionLegacyGuard.RequireNoLegacyAsync(events, source.Source, ct).ConfigureAwait(false);
        if (!legacy.IsSuccess)
            return Result<ArtifactRetentionSnapshot>.Failure(legacy.Error!);
        var first = await ReadOnceAsync(source, ct).ConfigureAwait(false);
        if (!first.IsSuccess)
            return first;
        var second = await ReadOnceAsync(source, ct).ConfigureAwait(false);
        return !second.IsSuccess ? second : first.Value.Stamp != second.Value.Stamp
            ? Changed() : first;
    }

    async ValueTask<Result<ArtifactRetentionSnapshot>> ReadOnceAsync(ArtifactRetentionSourceSnapshot source,
        CancellationToken ct)
    {
        var ids = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(source.Source.TenantId.ToString(),
                           ArtifactLegalHold.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit || record.Event is not ArtifactLegalHoldPlaced placed || placed.Source != source.Source)
                continue;
            var address = new EventStreamAddress(source.Source.TenantId.ToString(), ArtifactLegalHold.Area, placed.HoldId.ToString());
            if (record.Stream != address || record.Event.Metadata.AggregateId != placed.HoldId ||
                record.Event.Metadata.EventId == Uuid.Empty || !ids.Add(placed.HoldId))
                return Changed();
        }
        var basis = await aggregates.HydrateAsync(new ArtifactRetentionBasis(source.Source), ct).ConfigureAwait(false);
        // Copy all scalar/read values immediately: an adapter returning a reused mutable owner must not erase the fence.
        var basisFact = basis.Basis;
        var basisPosition = basis.CommittedStreamPosition;
        var basisRevision = basis.Revision;
        if (basisFact is { } recorded && (recorded.Source != source.Source || recorded.SourcePosition > source.Position ||
                recorded.BasisKind == "source_period" &&
                (recorded.PeriodStart != source.PeriodStart || recorded.PeriodEnd != source.PeriodEnd)))
            return Changed();
        var holds = new List<ArtifactLegalHoldView>();
        var stamp = new List<string>
        {
            source.Position.ToString(CultureInfo.InvariantCulture), basis.Stream.ToString(), basisPosition.ToString(CultureInfo.InvariantCulture),
            basisRevision.ToString(CultureInfo.InvariantCulture),
        };
        var revision = basisRevision;
        foreach (var id in ids.OrderBy(static id => id.ToString(), StringComparer.Ordinal))
        {
            var hold = await aggregates.HydrateAsync(new ArtifactLegalHold(source.Source.TenantId, id), ct).ConfigureAwait(false);
            if (hold.Placement is not { } placed || placed.Source != source.Source ||
                placed.SourcePosition > source.Position || hold.Release is { } released && released.SourcePosition > source.Position ||
                hold.View is not { } view)
                return Changed();
            holds.Add(view);
            revision = checked(revision + hold.Revision);
            stamp.Add(id.ToString());
            stamp.Add(hold.CommittedStreamPosition.ToString(CultureInfo.InvariantCulture));
            stamp.Add(hold.Revision.ToString(CultureInfo.InvariantCulture));
        }
        return Result<ArtifactRetentionSnapshot>.Success(new(source.Source, basisFact, Array.AsReadOnly(holds.ToArray()),
            revision, ContentDigest.Sha256(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", stamp)))));
    }

    static Result<ArtifactRetentionSnapshot> Changed() => Result<ArtifactRetentionSnapshot>.Failure(
        new RequestError(RequestErrorKind.Conflict, "Retention sources changed or their complete owner history is unavailable.", true));
}
