using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Deterministic identities, keys and provenance of the provider review process.</summary>
static class ProviderReviewEffects
{
    public const string ProcessName = "ProviderReviewProcess";

    /// <summary>The only generation of a review; a canceled review is never retried under its identity.</summary>
    public const long Generation = ProviderAssuranceReview.Generation;

    /// <summary>The schema of the immutable list of report grant facts the complete stage relies on.</summary>
    public const string UseProofSchema = "provider-review-use-proofs.v1";

    /// <summary>The reservation key of the use a review holds on one report.</summary>
    public static ReservationKey UseKey(Uuid reviewId, EventStreamAddress review, EventStreamAddress report) =>
        new(reviewId, review, (ulong)Generation, report,
            SourceUseReservations.PurposeName(SourceUsePurpose.ProviderReview));

    /// <summary>The requirement of the intent that names one report stream, or null when none is required.</summary>
    public static SourceUseRequirement? RequirementOf(IReadOnlyList<SourceUseRequirement> requirements,
        EventStreamAddress report) => requirements.SingleOrDefault(requirement => requirement.SourceOwner == report);

    /// <summary>The tenant a provider assurance stream belongs to.</summary>
    public static Uuid TenantOf(EventStreamAddress stream) => Uuid.Parse(stream.Realm,
        System.Globalization.CultureInfo.InvariantCulture);

    internal static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    internal static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}
