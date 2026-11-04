using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Projections;
namespace Bdgrz.Compliance.Features.Commitments.Projectors;

public sealed partial class CommitmentDraftDirectoryProjector(
    ICommitmentDraftDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("commitment-drafts"),
            FitzCommitmentDraftDirectory.ProjectorName),
      IProjectorHandler<CommitmentDraftCreated>, IProjectorHandler<CommitmentDraftRevised>,
      IProjectorHandler<CommitmentReviewed>, IProjectorHandler<CommitmentApproved>, IProjectorHandler<CommitmentRetired>
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

    public async ValueTask<Result> HandleAsync(IProjectorContext<CommitmentReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<CommitmentApproved> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<CommitmentRetired> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
