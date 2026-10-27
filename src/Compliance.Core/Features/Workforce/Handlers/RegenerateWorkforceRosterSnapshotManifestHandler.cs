using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

/// <summary>Regenerates a frozen roster's package manifest from its retained, verified rows.</summary>
public sealed class RegenerateWorkforceRosterSnapshotManifestHandler(IAggregateReader reader)
    : IRequestHandler<RegenerateWorkforceRosterSnapshotManifest,
        PopulationSnapshotManifestRegeneration>
{
    public ValueTask<Result<PopulationSnapshotManifestRegeneration>> HandleAsync(
        IRequestContext<RegenerateWorkforceRosterSnapshotManifest> context,
        CancellationToken ct) =>
        PopulationSnapshotManifest.RegenerateAsync(reader, context.Request.TenantId,
            context.Request.SnapshotId, WorkforceRosterSnapshotContent.Kind, ct);
}
