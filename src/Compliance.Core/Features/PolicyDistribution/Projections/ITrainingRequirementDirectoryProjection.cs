using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public interface ITrainingRequirementDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
