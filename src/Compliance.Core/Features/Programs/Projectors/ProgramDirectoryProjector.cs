using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Projections;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Projectors;

public sealed partial class ProgramDirectoryProjector(IProgramDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(), "ProgramDirectory"),
      IProjectorHandler<ProgramCreated>, IProjectorHandler<ProgramRevised>,
      IProjectorHandler<ProgramCriteriaEditionSelected>,
      IProjectorHandler<TypeIEntryDecisionRecorded>
{
    public ValueTask HandleAsync(ProgramCreated ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ProgramRevised ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ProgramCriteriaEditionSelected ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(TypeIEntryDecisionRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
