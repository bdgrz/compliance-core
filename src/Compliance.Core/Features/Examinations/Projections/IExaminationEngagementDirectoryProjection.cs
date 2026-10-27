using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Projections;

public interface IExaminationEngagementDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
