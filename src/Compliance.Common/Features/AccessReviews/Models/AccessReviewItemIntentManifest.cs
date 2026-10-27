using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     The complete, immutable selection of one launch: every item in canonical item order with the reference to its
///     frozen creation intent, with no count cap.
/// </summary>
public sealed record AccessReviewItemIntentManifest(IReadOnlyList<AccessReviewItemSelection> Selections)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "AccessReviewCampaignLaunched.ItemIntentManifest.v1";
}
