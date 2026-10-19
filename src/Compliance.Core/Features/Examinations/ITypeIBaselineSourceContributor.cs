using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Contributes one authoritative source family to a Type I baseline preview.</summary>
public interface ITypeIBaselineSourceContributor
{
    string Family { get; }

    ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct);

    ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct) => ReadAsync(engagement, ct);
}
