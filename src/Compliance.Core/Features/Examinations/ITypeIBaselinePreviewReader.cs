using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public interface ITypeIBaselinePreviewReader
{
    ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        CancellationToken ct = default);
}
