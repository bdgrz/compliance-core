using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

[Discriminator("bdgrz.commitment.draft.create", 2)]
public sealed record CreateCommitmentDraft(Uuid DraftId, Uuid TenantId, Uuid ProgramId, Uuid ServiceId,
    string Kind, string Identifier, string Statement, string Context, string SourceReference,
    Uuid? ProviderId = null)
    : IRequest<CommitmentDraftRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => DraftId;
}
