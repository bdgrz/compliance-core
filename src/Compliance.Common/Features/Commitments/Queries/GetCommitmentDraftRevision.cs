using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Commitments.Queries;

[Discriminator("bdgrz.commitment.draft.revision.get", 1)]
public sealed record GetCommitmentDraftRevision(Uuid TenantId, Uuid ProgramId,
    Uuid DraftId, long Revision)
    : IRequest<CommitmentDraftRevisionView>, IProgramScopedRequest, ICallable;
