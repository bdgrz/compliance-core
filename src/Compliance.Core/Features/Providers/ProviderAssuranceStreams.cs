using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Stream identities of the four provider assurance owners. They share one stream area per tenant so a single
///     projection checkpoint covers the records; every record still has its own stream, so independent reports,
///     reviews, gaps and links never contend and none is bounded by a tenant-wide history.
/// </summary>
public static class ProviderAssuranceStreams
{
    /// <summary>The stream area every provider assurance owner shares inside its tenant realm.</summary>
    public const string Area = "provider-assurance";

    /// <summary>The stream of one assurance report of one provider.</summary>
    public static EventStreamAddress Report(Uuid tenantId, Uuid providerId, Uuid reportId) =>
        Address(tenantId, ReportIdentity(tenantId, providerId, reportId));

    /// <summary>The stream of one personal provider review.</summary>
    public static EventStreamAddress Review(Uuid tenantId, Uuid reviewId) =>
        Address(tenantId, ReviewIdentity(tenantId, reviewId));

    /// <summary>The stream of one provider coverage gap.</summary>
    public static EventStreamAddress Gap(Uuid tenantId, Uuid providerId, Uuid gapId) =>
        Address(tenantId, GapIdentity(tenantId, providerId, gapId));

    /// <summary>The stream of one immutable accepted-risk link.</summary>
    public static EventStreamAddress RiskAcceptance(Uuid tenantId, Uuid linkId) =>
        Address(tenantId, RiskAcceptanceIdentity(tenantId, linkId));

    internal static Uuid ReportIdentity(Uuid tenantId, Uuid providerId, Uuid reportId) =>
        AggregateIdentity.Create(nameof(ProviderAssuranceReport), tenantId, providerId, reportId);

    internal static Uuid ReviewIdentity(Uuid tenantId, Uuid reviewId) =>
        AggregateIdentity.Create(nameof(ProviderAssuranceReview), tenantId, reviewId);

    internal static Uuid GapIdentity(Uuid tenantId, Uuid providerId, Uuid gapId) =>
        AggregateIdentity.Create(nameof(ProviderAssuranceGap), tenantId, providerId, gapId);

    internal static Uuid RiskAcceptanceIdentity(Uuid tenantId, Uuid linkId) =>
        AggregateIdentity.Create(nameof(ProviderRiskAcceptance), tenantId, linkId);

    static EventStreamAddress Address(Uuid tenantId, Uuid identity) =>
        new(tenantId.ToString(), Area, identity.ToString());
}
