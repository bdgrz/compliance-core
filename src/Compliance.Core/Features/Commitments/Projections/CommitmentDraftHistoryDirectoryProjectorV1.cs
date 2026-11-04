using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.History;
using Bdgrz.Compliance.Features.Commitments.Events;
namespace Bdgrz.Compliance.Features.Commitments.Projections;

public sealed partial class CommitmentDraftHistoryDirectoryProjectorV1(
    ICommitmentDraftHistoryDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("commitment-drafts"),
            FitzCommitmentDraftHistoryDirectoryV1.ProjectorName),
      IProjectorHandler<CommitmentDraftCreated>, IProjectorHandler<CommitmentDraftRevised>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<CommitmentDraftCreated> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<CommitmentDraftRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
