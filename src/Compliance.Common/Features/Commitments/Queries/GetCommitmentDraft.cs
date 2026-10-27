using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Commitments.Queries;

[Discriminator("bdgrz.commitment.draft.get", 1)]
public sealed record GetCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long? MinimumRevision = null)
    : IRequest<CommitmentDraftView>, IProgramScopedRequest, ICallable;
