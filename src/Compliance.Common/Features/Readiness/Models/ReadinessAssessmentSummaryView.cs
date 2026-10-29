using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Readiness.Models;

public sealed record ReadinessAssessmentSummaryView(Uuid AssessmentId, string RuleVersion,
    DateTimeOffset AsOf, int RuleMetCount, int GapCount, ActorReference RunBy,
    DateTimeOffset RunAt, string? DecisionOutcome);
