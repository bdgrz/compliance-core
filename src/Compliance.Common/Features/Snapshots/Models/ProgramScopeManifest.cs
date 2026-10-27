using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots.Models;

public sealed record ProgramScopeManifest(int FormatVersion, Uuid TenantId, Uuid ProgramId,
    long ProgramRevision, string ProgramContentSha256, Uuid BoundaryId,
    Uuid ApprovedBoundaryVersionId, string BoundaryContentSha256);
