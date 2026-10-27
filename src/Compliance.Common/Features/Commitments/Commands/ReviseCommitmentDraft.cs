using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.revise", 2)]
public sealed record ReviseCommitmentDraft(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long ExpectedRevision, string Statement, string Context, string SourceReference)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
