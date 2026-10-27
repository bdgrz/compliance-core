using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Complete original approved source identity and exact source bytes frozen before technical acceptance.</summary>
public sealed record SystemDescriptionExportIntent(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, Uuid ProcessId,
    Uuid VersionId, long Version, string Format, string ExportKey, string ContentSha256, ImmutableContentRef SourceContent,
    NativeFactReference VersionSource, NativeFactReference ApprovalSource, OperationProvenance Operation)
{
    public const string Schema = "system-description-export-intent.v1";
}
