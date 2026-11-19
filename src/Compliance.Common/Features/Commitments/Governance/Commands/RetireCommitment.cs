using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Commands;
[Discriminator("bdgrz.commitment.governance.retirecommitment", 1)]
public sealed record RetireCommitment(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, long ExpectedRevision, DateOnly EffectiveOn, string Reason)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
