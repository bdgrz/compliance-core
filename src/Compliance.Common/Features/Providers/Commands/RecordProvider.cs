using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Models;
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
