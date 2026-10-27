using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

interface IActualStaffEngagementLocatorReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);
    ValueTask<Page<ActualStaffEngagementLocatorView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default);
    ValueTask<Page<ActualStaffEngagementLocatorView>> ListForStaffAsync(Uuid tenantId,
        Uuid staffMemberId, Uuid canonicalUserId, int limit, string? cursor, CancellationToken ct = default);
}
