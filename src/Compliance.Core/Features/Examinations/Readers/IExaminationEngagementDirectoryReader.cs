using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Readers;

public interface IExaminationEngagementDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);

    ValueTask<ExaminationEngagementView?> GetAsync(Uuid tenantId, Uuid examinationEngagementId,
        CancellationToken ct = default);

    ValueTask<TypeIBaselineView?> GetBaselineAsync(Uuid tenantId, Uuid examinationEngagementId, Uuid typeIBaselineId,
        CancellationToken ct = default);

    /// <summary>At most two exact owning baseline matches; two matches retain ambiguity without choosing an engagement.</summary>
    ValueTask<IReadOnlyList<TypeIBaselineView>> FindBaselinesAsync(Uuid tenantId, Uuid programId, Uuid typeIBaselineId,
        CancellationToken ct = default);
}
