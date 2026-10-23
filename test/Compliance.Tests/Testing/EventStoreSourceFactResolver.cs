using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
/// Source fact resolver over an in-memory event store. The store reports only resource offsets, so realm and global
/// positions are synthesised deterministically in distinct ranges to make offset confusion visible.
/// </summary>
sealed class EventStoreSourceFactResolver(IDomainEventReader events) : ISourceFactResolver
{
    public const ulong RealmBase = 10_000;
    public const ulong GlobalBase = 1_000_000;

    public async Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong resourcePosition,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            await foreach (var record in events.ReadAsync(stream, resourcePosition, ct))
            {
                if (record.ResourceOffset != resourcePosition)
                    continue;
                if (record.Stream != stream)
                    throw new SourceFactVerificationException("The record was returned for a different stream.");
                return SourceFactVerifier.Build(stream, resourcePosition, RealmBase + resourcePosition,
                    GlobalBase + resourcePosition, record.Event);
            }
        }
        catch (InvalidOperationException)
        {
            // The in-memory store reports an unknown stream or offset beyond its end this way.
        }
        throw new SourceFactVerificationException($"No record exists at {stream} offset {resourcePosition}.");
    }

    public async Task<VerifiedSourceProof<TEvent>> VerifyAsync<TEvent>(SourceFactProof<TEvent> proof,
        CancellationToken ct = default) where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(proof);
        var fact = await ResolveAsync(proof.Source, proof.PhysicalPosition, ct).ConfigureAwait(false);
        return SourceFactVerifier.Verify(proof, fact);
    }
}
