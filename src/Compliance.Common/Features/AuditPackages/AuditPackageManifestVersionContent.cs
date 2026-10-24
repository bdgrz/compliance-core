using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>One complete immutable manifest version, with no builds, decisions, deliveries or unrelated history.</summary>
public sealed record AuditPackageManifestVersionContent(Uuid ProgramId, AuditPackageContent Content,
    AuditPackageManifestView Manifest, int? AmendsVersion, string? AmendmentReason, ActorReference CreatedBy,
    DateTimeOffset CreatedAt);
