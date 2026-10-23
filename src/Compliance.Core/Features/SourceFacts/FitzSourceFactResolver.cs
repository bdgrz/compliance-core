using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>
/// Resolves source facts through <see cref="IStreamClient"/>, the only Portia-adjacent surface that reports the
/// realm and global offsets; <c>DomainEventRecord</c> exposes only the resource offset.
/// </summary>
sealed class FitzSourceFactResolver(IStreamClient streams, IDomainEventSerializer serializer) : ISourceFactResolver
{
    public async Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong resourcePosition,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var route = stream.ToString();
        var page = await streams.ReadPageAsync(route, resourcePosition, 1, new StreamFilterSet(), null, null, null, ct)
            .ConfigureAwait(false);
        var record = page.Items
            .Where(item => item.Kind == StreamReadItemKind.Event)
            .Select(item => item.Record)
            .FirstOrDefault(candidate => candidate?.Offset == resourcePosition)
            ?? throw new SourceFactVerificationException($"No record exists at {route} offset {resourcePosition}.");
        if (!string.Equals(record.Route, route, StringComparison.Ordinal))
            throw new SourceFactVerificationException("The record was returned for a different stream.");
        if (record.RealmOffset is not { } realmOffset || record.GlobalOffset is not { } globalOffset)
            throw new SourceFactVerificationException("The broker did not report realm and global offsets.");
        DomainEvent domainEvent;
        try
        {
            domainEvent = serializer.Deserialize(record.Body);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SourceFactVerificationException($"The source record cannot be deserialized: {exception.Message}");
        }
        return SourceFactVerifier.Build(stream, resourcePosition, realmOffset, globalOffset, domainEvent);
    }

    public async Task<VerifiedSourceProof<TEvent>> VerifyAsync<TEvent>(SourceFactProof<TEvent> proof,
        CancellationToken ct = default) where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(proof);
        var fact = await ResolveAsync(proof.Source, proof.PhysicalPosition, ct).ConfigureAwait(false);
        return SourceFactVerifier.Verify(proof, fact);
    }
}
