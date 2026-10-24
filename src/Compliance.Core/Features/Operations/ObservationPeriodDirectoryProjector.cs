using Bdgrz.Compliance.Features.AuditPopulations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

public sealed partial class ObservationPeriodDirectoryProjector(IObservationPeriodDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(TypeIIPeriod.Area),
        FitzObservationPeriodDirectory.ProjectorName), IProjectorHandler<ObservationPeriodStarted>, IProjectorHandler<ObservationPeriodCloseFrozen>, IProjectorHandler<TypeIIPeriodCloseAmended>
{
    public ValueTask HandleAsync(ObservationPeriodStarted fact, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(fact, ct);
    public ValueTask HandleAsync(ObservationPeriodCloseFrozen fact, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(fact, ct);
    public ValueTask HandleAsync(TypeIIPeriodCloseAmended fact, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(fact, ct);
}
