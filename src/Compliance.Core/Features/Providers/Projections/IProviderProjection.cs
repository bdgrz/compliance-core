using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public interface IProviderProjection : IProjectionStore
{
    /// <summary>Projects an applied provider fact with the complete normalized content it references.</summary>
    ValueTask ApplyAsync(DomainEvent domainEvent, ProviderContent content, CancellationToken ct = default);
}
