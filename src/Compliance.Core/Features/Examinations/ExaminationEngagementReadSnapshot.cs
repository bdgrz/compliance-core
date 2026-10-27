using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Explicit verified read/rules composition, outside Aggregate command authority.</summary>
public sealed record ExaminationEngagementReadSnapshot(ExaminationEngagement Owner,
    ExaminationEngagementView? View, TypeIBaselineView? Baseline, ExaminationEngagementHistorySnapshot History,
    ExaminationEngagementRules Rules);
