using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Commitments.Commands;

[Discriminator("bdgrz.commitment.draft.revise", 2)]
public sealed record ReviseCommitmentDraft(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long ExpectedRevision, string Statement, string Context, string SourceReference)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
