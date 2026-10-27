using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>
///     Records authored provider facts; this grants no assurance or scope approval. <paramref name="ProviderId" /> is
///     the stable identity of the provider and the logical identity of the creation; an exact retry replays the
///     original acceptance. The answer is the fixed 202 acknowledgment while the name claim and the record fact follow.
/// </summary>
[Discriminator("bdgrz.provider.record", 1)]
public sealed record RecordProvider(Uuid ProviderId, Uuid TenantId, ProviderContent Content)
    : IRequest<OperationAcceptedResponse>, IProviderManagementRequest, IClientManagementMutationRequest, ICallable,
        IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ProviderId;
}
