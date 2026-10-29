using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

public interface IMemberAccessReader
{
    ValueTask<IReadOnlyList<MemberAccessEdge>> ReadAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct = default);
}
