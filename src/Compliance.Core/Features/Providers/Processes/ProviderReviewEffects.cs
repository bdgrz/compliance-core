using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Providers.Processes;

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
