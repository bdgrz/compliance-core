using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.SystemDescription.Processes;

/// <summary>Verified output bound to one exact approved source and original generated metadata; no access ticket is durable.</summary>
public sealed record SystemDescriptionExportOutput(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, Uuid ProcessId,
    Uuid VersionId, long Version, string Format, string ExportKey, string ContentSha256, ImmutableContentRef SourceContent,
    NativeFactReference VersionSource, NativeFactReference ApprovalSource, ArtifactContentReference Artifact, string MediaType,
    SystemDescriptionExportView Export)
{
    public const string Schema = "system-description-export-output.v1";
}
