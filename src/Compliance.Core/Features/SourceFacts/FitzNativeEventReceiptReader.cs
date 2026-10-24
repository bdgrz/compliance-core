using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>Retains Fitz's broker commit timestamp discarded by Portia0.9.1, bound to the complete exact native source tuple and payload.</summary>
sealed class FitzNativeEventReceiptReader(IStreamClient streams, IDomainEventSerializer serializer) : INativeEventReceiptReader
{
    public async ValueTask<DateTimeOffset> RecordedAtAsync(DomainEventRecord source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Event.Metadata is not { IsAudit: false } expected || expected.EventId == Uuid.Empty || expected.AggregateVersion == 0)
            throw new ImmutableContentIntegrityException("A receipt requires an exact committed non-audit native event identity and version.");
        var page = await streams.ReadPageAsync(source.Stream.ToString(), source.ResourceOffset, 1, new StreamFilterSet(), ct: ct).ConfigureAwait(false);
        if (page.Items.Count == 0 || page.Items.Count == 1 && page.Items[0].Kind != StreamReadItemKind.Event)
            throw new ImmutableContentUnavailableException("The exact native event receipt is not readable from the broker yet.");
        if (page.Items.Count != 1 || page.Items[0].Record is not { } record || record.Route != source.Stream.ToString() || record.Offset != source.ResourceOffset)
            throw new ImmutableContentIntegrityException("The broker receipt resolved on another concrete stream or resource offset.");
        DomainEvent actual;
        try
        { actual = serializer.Deserialize(record.Body); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { throw new ImmutableContentIntegrityException("The broker receipt payload cannot be decoded as its exact native event."); }
        if (actual.GetType() != source.Event.GetType() || actual.Metadata is not { IsAudit: false } observed ||
            observed != expected ||
            EventKind.Of(actual) != EventKind.Of(source.Event) || ContentDigest.Sha256(actual, actual.GetType()) != ContentDigest.Sha256(source.Event, source.Event.GetType()))
            throw new ImmutableContentIntegrityException("The broker receipt differs from the exact native event identity, version, kind or payload digest.");
        if (record.Timestamp == 0 || record.Timestamp > 253402300799999UL)
            throw new ImmutableContentIntegrityException("The broker receipt has no supported commit timestamp.");
        return DateTimeOffset.FromUnixTimeMilliseconds(checked((long)record.Timestamp));
    }
}
