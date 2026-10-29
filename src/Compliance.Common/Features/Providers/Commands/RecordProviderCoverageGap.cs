using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>Records a provider coverage gap. <paramref name="GapId" /> is the logical identity of the creation.</summary>
[Discriminator("bdgrz.provider.coverage_gap.record", 1)]
public sealed record RecordProviderCoverageGap(Uuid GapId, Uuid TenantId, Uuid ProviderId,
    ProviderCoverageGapContent Content) : IRequest<ProviderCoverageGapRegistration>,
    IProviderManagementRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => GapId;
}
