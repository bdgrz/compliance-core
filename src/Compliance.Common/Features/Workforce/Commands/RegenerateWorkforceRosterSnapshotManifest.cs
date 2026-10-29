using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>Recomputes a frozen roster's canonical package manifest from its retained rows.</summary>
[Discriminator("bdgrz.snapshot.workforce_roster.manifest_regenerate", 1)]
public sealed record RegenerateWorkforceRosterSnapshotManifest(Uuid TenantId, Uuid SnapshotId)
    : IRequest<PopulationSnapshotManifestRegeneration>, IWorkforceRequest, ICallable;
