using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Transient complete source slots and publication order, never retained by command authority.</summary>
public sealed class ApplicationImportSourceHistorySnapshot
{
    readonly Dictionary<Uuid, ApplicationImportSourceSlot> _slots;
    readonly EventStreamAddress _stream;
    readonly ulong _version;
    readonly ulong _position;
    readonly string _digest;

    internal ApplicationImportSourceHistorySnapshot(ApplicationImportSource source, IReadOnlyList<DomainEvent> facts,
        Dictionary<Uuid, ApplicationImportSourceSlot> slots, IReadOnlyList<ApplicationImportSourceSettled> published)
    {
        _slots = new(slots);
        (_stream, _version, _position, _digest) = (source.Stream, source.Version, source.CommittedStreamPosition, source.NativeCut.Digest);
        Facts = Array.AsReadOnly(facts.ToArray());
        Published = Array.AsReadOnly(published.ToArray());
    }
    public IReadOnlyList<DomainEvent> Facts { get; }
    public IReadOnlyList<ApplicationImportSourceSettled> Published { get; }
    public ApplicationImportSourceSlot? Find(Uuid batchId) => _slots.GetValueOrDefault(batchId);
    public bool Matches(ApplicationImportSource source) => _stream == source.Stream && _version == source.Version &&
        _position == source.CommittedStreamPosition && _digest == source.NativeCut.Digest;
}
