using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Workforce.Projections;

public interface IWorkforceSourceProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
