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
    public ValueTask HandleAsync(CommitmentDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(CommitmentDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
