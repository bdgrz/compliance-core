using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Work.Projections;
namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed partial class EvidenceWorkItemProjector(IEvidenceWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("evidence-requests"),
        FitzEvidenceWorkItemDirectory.ProjectorName),
      IProjectorHandler<EvidenceRequestOpened>, IProjectorHandler<EvidenceRequestFulfilled>,
      IProjectorHandler<EvidenceRequestCancelled>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<EvidenceRequestOpened> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<EvidenceRequestFulfilled> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<EvidenceRequestCancelled> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
