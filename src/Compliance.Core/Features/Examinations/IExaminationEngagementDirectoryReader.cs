using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public interface IExaminationEngagementDirectoryReader
{
    ValueTask<ExaminationEngagementView?> GetAsync(Uuid tenantId, Uuid examinationEngagementId,
        CancellationToken ct = default);
}
