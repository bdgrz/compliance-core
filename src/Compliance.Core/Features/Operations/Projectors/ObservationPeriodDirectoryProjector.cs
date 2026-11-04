using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Projections;
namespace Bdgrz.Compliance.Features.Operations.Projectors;

public sealed partial class ObservationPeriodDirectoryProjector(IObservationPeriodDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(TypeIIPeriod.Area),
        FitzObservationPeriodDirectory.ProjectorName), IProjectorHandler<ObservationPeriodStarted>, IProjectorHandler<ObservationPeriodCloseFrozen>, IProjectorHandler<TypeIIPeriodCloseAmended>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<ObservationPeriodStarted> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        await projection.ApplyAsync(fact, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<ObservationPeriodCloseFrozen> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        await projection.ApplyAsync(fact, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<TypeIIPeriodCloseAmended> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        await projection.ApplyAsync(fact, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
