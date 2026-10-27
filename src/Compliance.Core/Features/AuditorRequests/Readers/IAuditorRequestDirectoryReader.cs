using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Discovers created request identities and every scoped owner of a local selection identity.</summary>
public interface IAuditorRequestDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);
    ValueTask<Page<AuditorRequestSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default);
    ValueTask<IReadOnlyList<AuditorSampleSelectionLocator>> FindSelectionAsync(Uuid tenantId, Uuid programId,
        Uuid selectionId, CancellationToken ct = default);
}
