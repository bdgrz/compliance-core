using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Observes the current committed edition head at one point in time.</summary>
/// <remarks>The subsequent write must still compare the exact expected position; this does not certify rollback recovery.</remarks>
public interface ICriteriaFreshEditionHead
{
    /// <summary>Certifies the complete exact source through the next resource offset.</summary>
    ValueTask<bool> IsCurrentAsync(EventStreamAddress stream, Uuid aggregateId, ulong nextResourceOffset,
        CancellationToken ct = default);
}
