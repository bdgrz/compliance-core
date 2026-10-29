using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Queries;
namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class GetCommitmentVersionHandler(CommitmentVersionReadConsistency consistency)
    : IRequestHandler<GetCommitmentVersion, CommitmentVersionView>
{
    public ValueTask<Result<CommitmentVersionView>> HandleAsync(
        IRequestContext<GetCommitmentVersion> context, CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.DraftId, context.Request.Version, ct);
}
