using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>One exact captured native stream and its verified receipt prefix, independent of mutable source presentation.</summary>
public sealed class RecordedSourceReceiptPrefix(EventStreamAddress stream, IReadOnlyList<DomainEventRecord> records, IReadOnlySet<Uuid> included, IReadOnlyDictionary<Uuid, DateTimeOffset> recordedAt)
{
    public EventStreamAddress Stream { get; } = stream;
    public IReadOnlyList<DomainEventRecord> Records { get; } = records;
    public IEnumerable<DomainEventRecord> Included => Records.Where(record => included.Contains(record.Event.Metadata.EventId));
    public DateTimeOffset RecordedAt(DomainEvent source)
    {
        _ = Includes(source);
        return recordedAt[source.Metadata.EventId];
    }
    public bool Includes(DomainEvent source)
    {
        var metadata = source.Metadata;
        var actual = Records.SingleOrDefault(record => record.Event.Metadata.EventId == metadata.EventId);
        if (actual is null)
            throw new ImmutableContentUnavailableException("The selected source is absent from the captured native receipt stream.");
        if (actual.Event.GetType() != source.GetType() || actual.Event.Metadata != metadata ||
            ContentDigest.Sha256(actual.Event, actual.Event.GetType()) != ContentDigest.Sha256(source, source.GetType()))
            throw new ImmutableContentIntegrityException("The selected source differs from its exact captured native tuple and payload.");
        return included.Contains(metadata.EventId);
    }
    public bool Includes(NativeFactReference source)
    {
        if (source.Stream != Stream)
            throw new ImmutableContentIntegrityException("The selected source proof belongs to another exact stream.");
        var actual = Records.SingleOrDefault(record => record.Event.Metadata.EventId == source.EventId);
        if (actual is null)
            throw new ImmutableContentUnavailableException("The selected native source proof is absent from the captured receipt stream.");
        if (actual.Event.Metadata.AggregateVersion != source.AggregateVersion)
            throw new ImmutableContentIntegrityException("The selected source proof has another native version.");
        return included.Contains(source.EventId);
    }
    public bool Includes(Func<DomainEvent, bool> selected)
    {
        var found = Records.Where(record => selected(record.Event)).ToArray();
        if (found.Length == 0)
            throw new ImmutableContentUnavailableException("The selected source component is absent from its captured native receipt stream.");
        if (found.Length != 1)
            throw new ImmutableContentIntegrityException("The selected source component has more than one exact native fact.");
        return included.Contains(found[0].Event.Metadata.EventId);
    }
}
