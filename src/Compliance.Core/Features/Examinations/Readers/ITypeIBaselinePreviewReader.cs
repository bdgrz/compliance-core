using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Readers;

public interface ITypeIBaselinePreviewReader
{
    ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        CancellationToken ct = default);

    ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        Uuid requesterUserId, CancellationToken ct = default) => ReadAsync(engagement, ct);
}
