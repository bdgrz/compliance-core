using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     The frozen admission of one personal provider review. <paramref name="AuthoredReview" /> is the normalized
///     <see cref="RecordProviderReview" /> with its exact evidence, report, period, currency, cadence and rationale.
///     <paramref name="ReviewedReport" /> locates the exact report fact the review relies on, or is null when the
///     evidence is an externally cited classified citation. <paramref name="RequiredSourceUses" /> lists the source
///     owners held until the irreversible recording or the cancellation fence.
/// </summary>
public sealed record ProviderReviewIntent(DurableIntentHeader Header, Uuid ReviewId, Uuid ProviderId,
    ImmutableContentRef AuthoredReview, SourceFactLocator? ReviewedReport, ImmutableSelectionManifest RequiredSourceUses,
    DateTimeOffset ReviewedAt)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "provider-review-intent.v1";

    /// <summary>Schema of the normalized authored review.</summary>
    public const string AuthoredSchema = "RecordProviderReview";
}
