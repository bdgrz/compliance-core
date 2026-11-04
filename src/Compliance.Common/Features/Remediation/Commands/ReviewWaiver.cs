using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Remediation.Contracts;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>A personal review of the current requested waiver; HTTP only.</summary>
[Discriminator("bdgrz.waiver.review", 1)]
public sealed record ReviewWaiver(Uuid CommandId, Uuid TenantId, Uuid WaiverId,
    long ExpectedRevision, bool Approve, string? Comments = null) : IRequest, IIdentifiedRequest,
    IWaiverManagementRequest, IClientManagementMutationRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
