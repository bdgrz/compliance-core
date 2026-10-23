using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceFacts;

/// <summary>
/// Server-internal proof that a concrete native event was re-read and matched. Only this assembly can construct it,
/// so a request cannot manufacture one.
/// </summary>
/// <typeparam name="TFacts">The verified facts carried by the proof.</typeparam>
public sealed record VerifiedSourceProof<TFacts>
{
    internal VerifiedSourceProof(EventStreamAddress source, Uuid eventId, ulong physicalPosition,
        ulong ownerRevision, string payloadSha256, TFacts facts, ulong globalPosition, ulong realmPosition)
    {
        Source = source;
        EventId = eventId;
        PhysicalPosition = physicalPosition;
        OwnerRevision = ownerRevision;
        PayloadSha256 = payloadSha256;
        Facts = facts;
        GlobalPosition = globalPosition;
        RealmPosition = realmPosition;
    }

    /// <summary>The native stream.</summary>
    public EventStreamAddress Source { get; }

    /// <summary>The event id.</summary>
    public Uuid EventId { get; }

    /// <summary>The resource offset.</summary>
    public ulong PhysicalPosition { get; }

    /// <summary>The metadata replay version, distinct from any public logical revision.</summary>
    public ulong OwnerRevision { get; }

    /// <summary>SHA-256 over the concrete serialized payload.</summary>
    public string PayloadSha256 { get; }

    /// <summary>The verified facts.</summary>
    public TFacts Facts { get; }

    /// <summary>The global offset.</summary>
    public ulong GlobalPosition { get; }

    /// <summary>The realm offset.</summary>
    public ulong RealmPosition { get; }
}
