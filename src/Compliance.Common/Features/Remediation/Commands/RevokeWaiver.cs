using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Remediation.Contracts;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>A personal revocation of an approved, unexpired waiver; HTTP only.</summary>
[Discriminator("bdgrz.waiver.revoke", 1)]
public sealed record RevokeWaiver(Uuid CommandId, Uuid TenantId, Uuid WaiverId,
    long ExpectedRevision, string Reason) : IRequest, IIdentifiedRequest,
    IWaiverManagementRequest, IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
