using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Projections;
namespace Bdgrz.Compliance.Features.Risks.Projectors;

public sealed partial class RiskDraftDirectoryProjector(IRiskDraftDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("risks"),
            FitzRiskDraftDirectory.ProjectorName),
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
