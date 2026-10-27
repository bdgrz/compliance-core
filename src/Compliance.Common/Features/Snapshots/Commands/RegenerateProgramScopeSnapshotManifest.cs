using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots.Commands;

[Discriminator("bdgrz.snapshot.program_scope.manifest_regenerate", 1)]
public sealed record RegenerateProgramScopeSnapshotManifest(Uuid TenantId, Uuid SnapshotId)
    : IRequest<ProgramScopeSnapshotManifestRegeneration>, ISnapshotProgramResourceRequest, ICallable;
