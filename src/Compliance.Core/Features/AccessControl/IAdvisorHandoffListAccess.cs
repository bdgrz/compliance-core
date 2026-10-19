using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public interface IAdvisorHandoffListAccess
{
    ValueTask<AdvisorHandoffListVisibility> GetListVisibilityAsync(Uuid tenantId,
        Uuid programId, Uuid userId, CancellationToken ct = default);
}
