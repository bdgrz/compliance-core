using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public interface IComplianceIncidentDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
