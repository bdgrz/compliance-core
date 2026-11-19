using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Independently re-reads one exact committed participant fact through a qualified head.</summary>
/// <remarks>
/// An observed fact alone does not prove that its participant outcome is still current, that an
/// attempt was durably admitted, or that a write may be rebased. Callers must separately prove
/// those properties and conditionally append at their captured exact target position. Missing or
/// unqualified evidence returns null, never a refusal. The read streams records without retaining
/// a participant history. This verifier has no default source-qualification provider.
/// </remarks>
public sealed class PlatformOperatorExactSourceVerifier(IPlatformOperatorQualifiedSource? source = null)
{
    /// <summary>Returns the exact typed fact only after qualifying its complete source read.</summary>
    public async ValueTask<TEvent?> FindAsync<TEvent>(EventStreamAddress stream, Uuid aggregateId, Uuid eventId,
        CancellationToken ct = default) where TEvent : DomainEvent
    {
        if (source is null || aggregateId == Uuid.Empty || eventId == Uuid.Empty)
            return null;
        try
        {
            await using var session = await source.OpenAsync(stream, aggregateId, ct).ConfigureAwait(false);
            if (session is null)
                return null;
            ulong next = 0;
            TEvent? found = null;
            await foreach (var record in session.ReadAsync(ct).ConfigureAwait(false))
            {
                if (record.Stream != stream || record.ResourceOffset != next || record.Event.Metadata.AggregateId != aggregateId)
                    return null;
                next = checked(next + 1);
                if (record.Event.Metadata.EventId != eventId)
                    continue;
                if (found is not null || record.Event.Metadata.IsAudit || record.Event is not TEvent exact)
                    return null;
                found = exact;
            }
            if (found is null || next != session.ExclusiveHead || !await session.IsCurrentAsync(ct).ConfigureAwait(false))
                return null;
            return found;
        }
        catch (FitzException) { return null; }
        catch (IOException) { return null; }
        catch (TimeoutException) { return null; }
        catch (OverflowException) { return null; }
        catch (InvalidOperationException) { return null; }
    }
}
