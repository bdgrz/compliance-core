using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Commitments.Queries;

[Discriminator("bdgrz.commitment.draft.revision.list", 1)]
public sealed record ListCommitmentDraftRevisions(Uuid TenantId, Uuid ProgramId,
    Uuid DraftId, int? Limit = null, string? Cursor = null,
    long? MinimumDraftRevision = null)
    : IRequest<Page<CommitmentDraftRevisionView>>, IProgramScopedRequest, ICallable;
