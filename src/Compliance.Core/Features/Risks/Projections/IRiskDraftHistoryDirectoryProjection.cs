using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks.Projections;

public interface IRiskDraftHistoryDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
