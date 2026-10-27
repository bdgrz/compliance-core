using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>An attributable internal review decision for one exact response version.</summary>
public sealed record AuditorResponseReviewView(
    string Outcome,
    string Rationale,
    Uuid ReviewerMemberId,
    string ReviewerDisplay,
    DateTimeOffset ReviewedAt);
