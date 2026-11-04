using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Commands;
[Discriminator("bdgrz.commitment.governance.draftcommitment", 1)]
public sealed record DraftCommitment(Uuid CommitmentId, Uuid TenantId, Uuid ProgramId, string Kind, string Identifier, CommitmentTerms Terms)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommitmentId;
}
