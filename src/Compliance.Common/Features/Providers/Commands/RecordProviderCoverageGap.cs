using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Records a provider coverage gap. <paramref name="GapId" /> is the logical identity of the creation.</summary>
[Discriminator("bdgrz.provider.coverage_gap.record", 1)]
public sealed record RecordProviderCoverageGap(Uuid GapId, Uuid TenantId, Uuid ProviderId,
    ProviderCoverageGapContent Content) : IRequest<ProviderCoverageGapRegistration>,
    IProviderManagementRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => GapId;
}
