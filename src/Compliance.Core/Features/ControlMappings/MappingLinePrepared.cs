using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>The exact hydrated append cut and prepared immutable native fact.</summary>
public sealed class MappingLinePrepared
{
    internal MappingLinePrepared(long beforeRevision, ulong ownerVersion, ulong physicalPosition, string replayDigest, DomainEvent fact) =>
        (BeforeRevision, OwnerVersion, PhysicalPosition, ReplayDigest, Fact) = (beforeRevision, ownerVersion, physicalPosition, replayDigest, fact);
    public long BeforeRevision { get; }
    public ulong OwnerVersion { get; }
    public ulong PhysicalPosition { get; }
    internal string ReplayDigest { get; }
    internal DomainEvent Fact { get; }
}
