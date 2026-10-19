using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Reads exact, effective subservice responsibilities captured for a Type I baseline.</summary>
public interface IProviderCsocBaselineReader
{
    ValueTask<Result<IReadOnlySet<Uuid>>> ReadEffectiveProviderCsocsAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct);
}
