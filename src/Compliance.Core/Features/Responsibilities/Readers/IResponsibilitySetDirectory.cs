using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Readers;

public interface IResponsibilitySetDirectory
{
    ValueTask<ResponsibilitySetView?> GetAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default);
}

public interface IResponsibilitySetProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
