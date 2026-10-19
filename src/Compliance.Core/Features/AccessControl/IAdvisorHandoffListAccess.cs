using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public interface IAdvisorHandoffListAccess
{
    ValueTask<bool> CanListSharedHandoffsAsync(Uuid tenantId, Uuid programId, Uuid userId,
        CancellationToken ct = default);
}
