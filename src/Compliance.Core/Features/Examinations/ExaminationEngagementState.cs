using Bdgrz.Compliance.Features.Examinations.Models;
namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>One complete immutable business snapshot; no lifetime history is embedded.</summary>
public sealed record ExaminationEngagementState(ExaminationEngagementView Engagement, TypeIBaselineView? Baseline)
{
    public const string Schema = "examination-engagement-state.v1";
}
