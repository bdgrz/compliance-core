using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>One complete immutable business snapshot; no lifetime history is embedded.</summary>
public sealed record ExaminationEngagementState(ExaminationEngagementView Engagement, TypeIBaselineView? Baseline)
{
    public const string Schema = "examination-engagement-state.v1";
}
