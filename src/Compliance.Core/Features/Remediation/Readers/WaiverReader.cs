using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Readers;

/// <summary>Reads the one waiver owned by the requested tenant; expiry is computed at read time.</summary>
public sealed class WaiverReader(IAggregateReader reader, TimeProvider clock)
{
    public async ValueTask<WaiverView?> ReadAsync(Uuid tenantId, Uuid waiverId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || waiverId == Uuid.Empty)
            return null;
        var owner = await reader.HydrateAsync(new Waiver(tenantId, waiverId), ct).ConfigureAwait(false);
        return owner.ViewOrNull(clock.GetUtcNow());
    }
}
