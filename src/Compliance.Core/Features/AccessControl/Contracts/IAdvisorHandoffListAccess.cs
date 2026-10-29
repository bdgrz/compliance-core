using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

public interface IAdvisorHandoffListAccess
{
    ValueTask<AdvisorHandoffListVisibility> GetListVisibilityAsync(Uuid tenantId,
        Uuid programId, Uuid userId, CancellationToken ct = default);
}
