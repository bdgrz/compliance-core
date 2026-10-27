using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures effective, separately approved system requirements for a Type I baseline.</summary>
public sealed class SystemRequirementTypeIBaselineSourceContributor(
    CommitmentTypeIBaselineSourceReader reader) : ITypeIBaselineSourceContributor
{
    static readonly IReadOnlySet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "system_requirement",
    };

    public string Family => "system_requirements";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        reader.ReadAsync(Family, Kinds, engagement, ct);

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct) => reader.ReadAsync(Family, Kinds, engagement, context, ct);
}
