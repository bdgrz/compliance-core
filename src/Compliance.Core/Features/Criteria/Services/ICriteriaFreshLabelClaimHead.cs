using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Certifies the exact current head and conditional-write lineage for a label claim.</summary>
/// <remarks>
/// A true response observes the supplied head in a contiguous committed read.
/// A later append must still use that exact expected position. An ambiguous prior
/// append requires reconciliation or a same-position fence; this contract does
/// not establish rollback or recovery guarantees beyond the event store.
/// </remarks>
public interface ICriteriaFreshLabelClaimHead
{
    /// <summary>Qualifies the exact physical source head of one label claim.</summary>
    ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
        CancellationToken ct = default);
}
