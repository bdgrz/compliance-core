using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.ControlMappings.Services;

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
