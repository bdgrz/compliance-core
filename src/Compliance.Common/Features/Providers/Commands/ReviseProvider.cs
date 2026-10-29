using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>
///     Appends authored provider facts at the expected revision. <paramref name="CommandId" /> is the logical identity
///     of the operation. The answer is the fixed 202 acknowledgment while the name claim and the record fact follow.
/// </summary>
[Discriminator("bdgrz.provider.revise", 1)]
public sealed record ReviseProvider(Uuid CommandId, Uuid TenantId, Uuid ProviderId, long ExpectedRevision,
    ProviderContent Content)
    : IRequest<OperationAcceptedResponse>, IProviderManagementRequest, IClientManagementMutationRequest, ICallable,
        IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
