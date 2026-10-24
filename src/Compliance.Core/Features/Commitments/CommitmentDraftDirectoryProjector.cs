using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed partial class CommitmentDraftDirectoryProjector(
    ICommitmentDraftDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("commitment-drafts"),
            FitzCommitmentDraftDirectory.ProjectorName),
      IProjectorHandler<CommitmentDraftCreated>, IProjectorHandler<CommitmentDraftRevised>,
      IProjectorHandler<CommitmentReviewed>, IProjectorHandler<CommitmentApproved>, IProjectorHandler<CommitmentRetired>
{
    public ValueTask HandleAsync(CommitmentDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CommitmentDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CommitmentReviewed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CommitmentApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
    public ValueTask HandleAsync(CommitmentRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
