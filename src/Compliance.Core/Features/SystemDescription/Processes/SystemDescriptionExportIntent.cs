using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Processes;

/// <summary>Complete original approved source identity and exact source bytes frozen before technical acceptance.</summary>
public sealed record SystemDescriptionExportIntent(Uuid TenantId, Uuid ProgramId, Uuid DescriptionId, Uuid ProcessId,
    Uuid VersionId, long Version, string Format, string ExportKey, string ContentSha256, ImmutableContentRef SourceContent,
    NativeFactReference VersionSource, NativeFactReference ApprovalSource, OperationProvenance Operation)
{
    public const string Schema = "system-description-export-intent.v1";
}
