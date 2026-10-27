using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Complete build inputs frozen before queueing; BuildId identifies business intent, never a worker attempt.</summary>
public sealed record PackageBuildIntent(DurableIntentHeader Header, Uuid PackageId, Uuid ExaminationId,
    string PackageKind, long PackageVersion, Uuid BuildId, string BuildKey, Uuid ManifestId,
    string ManifestSha256, ImmutableContentRef Manifest, ImmutableSelectionManifest Selection)
{
    public const string Schema = "package-build-intent.v1";
}
