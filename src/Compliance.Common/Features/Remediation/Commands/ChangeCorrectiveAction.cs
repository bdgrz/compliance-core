using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>Changes an open action's description, owner and due date at the action's expected revision.</summary>
[Discriminator("bdgrz.finding.corrective_action.change", 1)]
public sealed record ChangeCorrectiveAction(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    Uuid ActionId, long ExpectedRevision, string Description, Uuid OwnerMemberId, DateOnly DueOn)
    : IRequest<FindingView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
