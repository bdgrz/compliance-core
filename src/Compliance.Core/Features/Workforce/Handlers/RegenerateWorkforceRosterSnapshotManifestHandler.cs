using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
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
