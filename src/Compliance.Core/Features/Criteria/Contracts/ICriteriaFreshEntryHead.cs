using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Contracts;

/// <summary>Qualifies the exact current head used for an entry write.</summary>
/// <remarks>
/// A true result means a committed contiguous read reached the supplied head at
/// a point in time under the configured event-store contract. It does not reserve
/// that head: a later append must still use the same expected physical position.
/// An unknown earlier append remains pending until a committed outcome or a
/// same-position durable fence settles it. This contract does not certify
/// historical rollback, storage replacement or disaster recovery.
/// </remarks>
public interface ICriteriaFreshEntryHead
{
    /// <summary>Certifies the exact source position for one named owner.</summary>
    ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
        CancellationToken ct = default);
}
