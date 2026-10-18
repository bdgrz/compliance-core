using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public interface IExaminationEngagementDirectoryReader
{
    ValueTask<ExaminationEngagementView?> GetAsync(Uuid tenantId, Uuid examinationEngagementId,
        CancellationToken ct = default);

    ValueTask<TypeIBaselineView?> GetBaselineAsync(Uuid tenantId, Uuid typeIBaselineId,
        CancellationToken ct = default);
}
