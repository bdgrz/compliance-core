using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Verified output bound to one exact approved source and original generated metadata; no access ticket is durable.</summary>
public sealed record SystemDescriptionExportOutput(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, Uuid ProcessId,
    Uuid VersionId, long Version, string Format, string ExportKey, string ContentSha256, ImmutableContentRef SourceContent,
    NativeFactReference VersionSource, NativeFactReference ApprovalSource, ArtifactContentReference Artifact, string MediaType,
    SystemDescriptionExportView Export)
{
    public const string Schema = "system-description-export-output.v1";
}
