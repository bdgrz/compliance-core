using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Commands;
[Discriminator("bdgrz.commitment.governance.confirmcommitmentcontrollink", 1)]
public sealed record ConfirmCommitmentControlLink(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, Uuid ControlId, long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
