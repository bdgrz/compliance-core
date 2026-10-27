using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Models;

/// <summary>
///     The frozen terms of one completion review: the exact submission under review, the authored decision (outcome,
///     rationale, reviewer and the exact waiver proof when one was required), the complete cited evidence selection and
///     the complete exact evidence source uses. Every review receiver copies it; none re-authors it from current state.
/// </summary>
public sealed record RiskActionCompletionReviewIntent(DurableIntentHeader Header, Uuid ActionId, Uuid SubmissionId,
    ImmutableContentRef AuthoredReview, ImmutableSelectionManifest CompletionEvidence,
    ImmutableSelectionManifest RequiredSourceUses)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "risk-action-completion-review-intent.v1";
}
