using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.Providers.Models;

/// <summary>
///     The immutable proof that fenced a pending review: the permanent source mismatch that makes it impossible to
///     record. It names the review only, so every retry of the abort writes identical content.
/// </summary>
public sealed record ProviderReviewFailureProof(Uuid ReviewId, string Reason)
{
    /// <summary>The reason recorded when the pinned report revision is no longer the held one.</summary>
    public const string SourceMismatch = "source_mismatch";
}
