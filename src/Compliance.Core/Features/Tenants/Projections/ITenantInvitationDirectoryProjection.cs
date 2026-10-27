using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Projections;

public interface ITenantInvitationDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
