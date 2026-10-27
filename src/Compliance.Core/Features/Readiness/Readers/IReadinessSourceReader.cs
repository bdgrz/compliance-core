using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Readiness.Readers;

/// <summary>Reads the program-scoped source records the readiness rules evaluate.</summary>
public interface IReadinessSourceReader
{
    ValueTask<Result<ReadinessSourceSet>> ReadAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset asOf, CancellationToken ct = default);
}
