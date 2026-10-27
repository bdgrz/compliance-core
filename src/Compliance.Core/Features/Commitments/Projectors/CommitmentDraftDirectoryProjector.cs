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
