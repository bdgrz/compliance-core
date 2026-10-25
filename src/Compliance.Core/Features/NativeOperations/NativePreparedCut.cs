using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>The exact committed owner prefix against which immutable command preparation ran.</summary>
sealed record NativePreparedCut(EventStreamAddress Owner, Uuid AggregateId, ulong NativeVersion,
    ulong PhysicalPosition, string PayloadSha256, string MetadataSha256)
{
    public static NativePreparedCut Of(Aggregate owner, string payload, string metadata) =>
        new(owner.Stream, owner.Id, owner.Version, owner.CommittedStreamPosition, payload, metadata);
}
