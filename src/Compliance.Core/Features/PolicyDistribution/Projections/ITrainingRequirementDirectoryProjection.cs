using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Projections;

public interface ITrainingRequirementDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
