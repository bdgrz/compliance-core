using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>Cancels open corrective work with a reason; the expected revision belongs to the action.</summary>
[Discriminator("bdgrz.finding.corrective_action.cancel", 1)]
public sealed record CancelCorrectiveAction(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, Uuid ActionId, string Reason)
    : IRequest<FindingView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
