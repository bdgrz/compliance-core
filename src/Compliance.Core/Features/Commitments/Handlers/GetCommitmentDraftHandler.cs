using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class GetCommitmentDraftHandler(CommitmentDraftReadConsistency consistency)
    : IRequestHandler<GetCommitmentDraft, CommitmentDraftView>
{
    public ValueTask<Result<CommitmentDraftView>> HandleAsync(
        IRequestContext<GetCommitmentDraft> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.DraftId, context.Request.MinimumRevision, ct);
}
