using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.History;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Risks.Projections;

public sealed partial class RiskDraftHistoryProjectorV1(
    IRiskDraftHistoryDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("risks"),
            FitzRiskDraftHistoryDirectoryV1.ProjectorName),
      IProjectorHandler<RiskDraftCreated>, IProjectorHandler<RiskDraftRevised>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<RiskDraftCreated> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<RiskDraftRevised> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
