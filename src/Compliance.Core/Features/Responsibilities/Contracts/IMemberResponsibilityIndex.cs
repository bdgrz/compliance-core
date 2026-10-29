using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Responsibilities.Contracts;

public interface IMemberResponsibilityIndex
{
    ValueTask<IReadOnlyList<ResponsibilityAssignmentView>> GetAsync(Uuid tenantId,
        Uuid memberId, CancellationToken ct = default);
}
