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
    public ValueTask HandleAsync(RiskDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
