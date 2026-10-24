using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>
/// Resolves source facts through a global selector, the only READ scope that reports global offsets. The concrete
/// source route and resource position must both match before a record is deserialized.
/// </summary>
sealed class FitzSourceFactResolver(IStreamClient streams, IDomainEventSerializer serializer) : ISourceFactResolver
{
    public async Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong resourcePosition,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var record = await FindAsync(stream, resourcePosition, ct).ConfigureAwait(false);
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

    async Task<StreamRecord> FindAsync(EventStreamAddress stream, ulong resourcePosition, CancellationToken ct)
    {
        var concrete = stream.ToString();
        var selector = $"stream://*/{stream.Area}/{stream.Resource}";
        ulong next = 0;
        ulong? fingerprint = null;
        ulong? watermark = null;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await streams.ReadPageAsync(selector, next, 256, new StreamFilterSet(), null, fingerprint,
                watermark, ct).ConfigureAwait(false);
            if (watermark is { } pinned && page.Cursor.CapturedWatermark != pinned)
                throw new SourceFactVerificationException("The source selector snapshot changed between pages.");
            var record = page.Items.Where(item => item.Kind == StreamReadItemKind.Event)
                .Select(item => item.Record).FirstOrDefault(candidate => candidate?.Offset == resourcePosition &&
                    string.Equals(candidate.Route, concrete, StringComparison.Ordinal));
            if (record is not null)
                return record;
            if (!page.Cursor.HasMore)
                throw new SourceFactVerificationException($"No record exists at {concrete} offset {resourcePosition}.");
            if (page.Cursor.LastGlobalOffset is not { } last || last < next || last == ulong.MaxValue ||
                page.Cursor.CursorFingerprint is not { } token || page.Cursor.CapturedWatermark is not { } captured)
                throw new SourceFactVerificationException("The source selector cannot make verified global progress.");
            next = last + 1;
            fingerprint = token;
            watermark = captured;
        }
    }

    public async Task<VerifiedSourceProof<TEvent>> VerifyAsync<TEvent>(SourceFactProof<TEvent> proof,
        CancellationToken ct = default) where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(proof);
        var fact = await ResolveAsync(proof.Source, proof.PhysicalPosition, ct).ConfigureAwait(false);
        return SourceFactVerifier.Verify(proof, fact);
    }
}
