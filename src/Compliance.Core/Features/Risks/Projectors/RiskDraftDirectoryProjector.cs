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
    public ValueTask HandleAsync(RiskDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(RiskDraftRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
