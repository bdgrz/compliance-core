using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     A verified completion review: the authoritative action, its frozen review intent and the authored decision.
///     Exactly one of <paramref name="Error" /> and the verified parts is set.
/// </summary>
public sealed record RiskActionReview(RequestError? Error, RiskTreatmentAction? Action,
    RiskActionCompletionReviewIntent? Intent, RiskActionReviewDecision? Decision, Uuid TenantId)
{
    /// <summary>A failed verification.</summary>
    public static RiskActionReview Failed(RequestError error) => new(error, null, null, null, Uuid.Empty);
}
