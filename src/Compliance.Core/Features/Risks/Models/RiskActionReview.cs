using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Risks.Aggregates;
namespace Bdgrz.Compliance.Features.Risks.Models;

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
