using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>The exact item fact the preview froze as the item's head: the fact a hold must find still current.</summary>
public sealed record BulkItemEligibilityProof(NativeFactReference ItemHead)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "bulk-access-decision-eligibility-proof.v1";
}
