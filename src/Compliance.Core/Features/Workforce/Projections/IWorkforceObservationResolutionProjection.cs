using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Workforce.Projections;

public interface IWorkforceObservationResolutionProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
