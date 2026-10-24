using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>
///     What a pair verified about its proven source acceptance, derived from the immutable workflow intent. A
///     non-null <paramref name="SupersededReason" /> means the source operation is already terminal and the receiver is
///     a no-op. <paramref name="Disposition" /> is the disposition the verified terminal source event kind requires; a
///     settlement carries it, an authorization does not.
/// </summary>
public sealed record SourceUseSource(DurableIntentHeader Header, ImmutableContentRef ParentIntent,
    ReservationKey Key, SourceUseRequirement Requirement, string CommandKind,
    SourceUseSettlementDisposition? Disposition, string? SupersededReason)
{
    /// <summary>A rejected source requires the process abort and settlement stages to recover any held uses.</summary>
    public bool RequiresAbort { get; init; }
}
