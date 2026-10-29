using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.Operations.Events;
namespace Bdgrz.Compliance.Features.Operations.Projections;

public interface IObservationPeriodDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(ObservationPeriodStarted fact, CancellationToken ct = default);
    ValueTask ApplyAsync(ObservationPeriodCloseFrozen fact, CancellationToken ct = default);
    ValueTask ApplyAsync(TypeIIPeriodCloseAmended fact, CancellationToken ct = default);
}
