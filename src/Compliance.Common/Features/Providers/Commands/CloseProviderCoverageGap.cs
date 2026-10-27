using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Providers.Models;
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
