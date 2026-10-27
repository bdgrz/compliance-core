using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class PreviewCommitmentImpactHandler(CommitmentImpactService service)
    : IRequestHandler<PreviewCommitmentImpact, CommitmentImpactPreview>
{
    public ValueTask<Result<CommitmentImpactPreview>> HandleAsync(
        IRequestContext<PreviewCommitmentImpact> context, CancellationToken ct) =>
        service.PreviewAsync(context.Request, ct);
}
