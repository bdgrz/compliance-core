using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Commands;
[Discriminator("bdgrz.commitment.governance.discardcommitmentdraft", 1)]
public sealed record DiscardCommitmentDraft(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
