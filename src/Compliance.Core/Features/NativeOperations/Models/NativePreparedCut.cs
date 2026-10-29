using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>The exact committed owner prefix against which immutable command preparation ran.</summary>
sealed record NativePreparedCut(EventStreamAddress Owner, Uuid AggregateId, ulong NativeVersion,
    ulong PhysicalPosition, string PayloadSha256, string MetadataSha256)
{
    public static NativePreparedCut Of(Aggregate owner, string payload, string metadata) =>
        new(owner.Stream, owner.Id, owner.Version, owner.CommittedStreamPosition, payload, metadata);
}
