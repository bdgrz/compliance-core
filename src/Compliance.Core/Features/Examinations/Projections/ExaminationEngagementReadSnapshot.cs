using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
namespace Bdgrz.Compliance.Features.Examinations.Projections;

/// <summary>Explicit verified read/rules composition, outside Aggregate command authority.</summary>
public sealed record ExaminationEngagementReadSnapshot(ExaminationEngagement Owner,
    ExaminationEngagementView? View, TypeIBaselineView? Baseline, ExaminationEngagementHistorySnapshot History,
    ExaminationEngagementRules Rules);
