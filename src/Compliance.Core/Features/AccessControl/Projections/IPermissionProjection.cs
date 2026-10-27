using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

public interface IPermissionProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
