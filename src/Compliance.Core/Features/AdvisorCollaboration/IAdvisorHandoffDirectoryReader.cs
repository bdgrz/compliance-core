using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

public interface IAdvisorHandoffDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);

    ValueTask<Page<AdvisorRequestSummaryView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default);

    ValueTask<Page<AdvisorRequestSummaryView>> ListSharedProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly asOf, int limit, string? cursor, CancellationToken ct = default);
}
