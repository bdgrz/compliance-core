using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Commands;
[Discriminator("bdgrz.commitment.governance.reviewcommitmentdraft", 1)]
public sealed record ReviewCommitmentDraft(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, long ExpectedRevision, string Outcome, string Rationale, DateOnly? EffectiveOn = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
