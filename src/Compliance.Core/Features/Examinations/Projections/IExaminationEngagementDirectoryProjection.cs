using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

public interface IExaminationEngagementDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
