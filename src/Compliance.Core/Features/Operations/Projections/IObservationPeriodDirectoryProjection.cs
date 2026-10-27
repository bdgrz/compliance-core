using Bdgrz.Compliance.Features.AuditPopulations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

public interface IObservationPeriodDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(ObservationPeriodStarted fact, CancellationToken ct = default);
    ValueTask ApplyAsync(ObservationPeriodCloseFrozen fact, CancellationToken ct = default);
    ValueTask ApplyAsync(TypeIIPeriodCloseAmended fact, CancellationToken ct = default);
}
