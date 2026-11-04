using Bdgrz.Compliance.Features.Readiness.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Readers;

/// <summary>The owning context computes its gaps from authoritative source; readiness records its answer.</summary>
public interface IReadinessComputedGapSource
{
    string Family { get; }
    ValueTask<Result<ReadinessComputedGapSet>> ReadAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset asOf, CancellationToken ct = default);
}
