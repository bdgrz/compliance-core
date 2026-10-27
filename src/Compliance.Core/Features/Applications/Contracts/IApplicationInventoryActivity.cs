using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Contracts;

public interface IApplicationInventoryActivity
{
    ValueTask<bool> IsDeclaredAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default);

    ValueTask<SystemInstanceReferenceState> GetInstanceStateAsync(Uuid tenantId, Uuid instanceId,
        CancellationToken ct = default);
}
