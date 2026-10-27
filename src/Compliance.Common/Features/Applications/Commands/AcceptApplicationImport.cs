using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Applications.Commands;

/// <summary>
///     Seals the complete frozen plan of a batch and starts <c>ApplicationImportExecutionProcess</c>. The command
///     identity is the logical operation; an exact retry replays the original 202 acknowledgment.
/// </summary>
[Discriminator("bdgrz.application_import.accept", 2)]
public sealed record AcceptApplicationImport(Uuid CommandId, Uuid TenantId, Uuid BatchId, long ExpectedRevision)
    : IRequest<OperationAcceptedResponse>, IApplicationInventoryWriteRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
