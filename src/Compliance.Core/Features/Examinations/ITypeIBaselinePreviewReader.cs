using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public interface ITypeIBaselinePreviewReader
{
    ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        CancellationToken ct = default);

    ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        Uuid requesterUserId, CancellationToken ct = default) => ReadAsync(engagement, ct);
}
