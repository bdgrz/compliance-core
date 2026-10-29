using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>An independent review decision on one exact immutable operating-plan version.</summary>
public sealed record TypeIIPlanReviewView(
    int PlanVersion,
    string Outcome,
    string Comments,
    Uuid ReviewerMemberId,
    string ReviewerDisplay,
    DateTimeOffset ReviewedAt);
