using Cntryl.Portia;
using Bdgrz.Compliance.Features.Snapshots.Contracts;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Commands;

[Discriminator("bdgrz.snapshot.program_scope.manifest_regenerate", 1)]
public sealed record RegenerateProgramScopeSnapshotManifest(Uuid TenantId, Uuid SnapshotId)
    : IRequest<ProgramScopeSnapshotManifestRegeneration>, ISnapshotProgramResourceRequest, ICallable;
