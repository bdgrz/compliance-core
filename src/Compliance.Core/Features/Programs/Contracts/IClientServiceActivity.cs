using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Contracts;

public interface IClientServiceActivity
{
    ValueTask<bool> IsActiveAsync(Uuid tenantId, Uuid programId, Uuid serviceId,
        CancellationToken ct = default);
}
