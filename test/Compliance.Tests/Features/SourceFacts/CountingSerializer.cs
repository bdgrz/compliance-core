using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.SourceFacts;

/// <summary>Counts deserialization calls so tests can prove foreign content is never materialized.</summary>
sealed class CountingSerializer(IDomainEventSerializer inner) : IDomainEventSerializer
{
    public int Deserializations { get; private set; }

    public ReadOnlyMemory<byte> Serialize(DomainEvent ev) => inner.Serialize(ev);

    public DomainEvent Deserialize(ReadOnlyMemory<byte> data)
    {
        Deserializations++;
        return inner.Deserialize(data);
    }
}
