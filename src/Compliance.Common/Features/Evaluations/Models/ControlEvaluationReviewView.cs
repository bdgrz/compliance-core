using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Models;

/// <summary>An independent review decision on one submitted round: accepted, rejected, or changes_requested.</summary>
public sealed record ControlEvaluationReviewView(Uuid DecisionId, int Round, string Decision,
    string Rationale, Uuid ReviewerMemberId, ActorReference ReviewedBy, DateTimeOffset ReviewedAt,
    Uuid? SeparationOfDutiesWaiverId = null);
