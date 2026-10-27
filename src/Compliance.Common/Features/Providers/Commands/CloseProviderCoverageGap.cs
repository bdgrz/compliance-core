using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>
///     Closes an open provider gap once, against newer or different coverage evidence, at the expected revision of
///     the gap owner. <paramref name="CommandId" /> is the logical identity of the closure.
/// </summary>
[Discriminator("bdgrz.provider.coverage_gap.close", 1)]
public sealed record CloseProviderCoverageGap(Uuid CommandId, Uuid TenantId, Uuid ProviderId, Uuid GapId,
    long ExpectedRevision, ProviderCoverageGapClosureContent Content)
    : IRequest<ProviderCoverageGapRegistration>, IProviderManagementRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
