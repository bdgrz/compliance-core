using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks.Projections;

public interface IRiskEvaluationDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
