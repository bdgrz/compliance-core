using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Exact source references and unresolved issues shown before baseline confirmation.</summary>
public sealed record TypeIBaselinePreview(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ExaminationEngagementId,
    IReadOnlyList<TypeIBaselineSourceReference> Sources,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> ScopeDifferences)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableContentRef? SourceContentManifest { get; init; }

    public IReadOnlyList<TypeIBaselineSourceCoverage> SourceCoverage { get; init; } = [];

    public string CanonicalManifest => TypeIBaselineContentIdentity.Manifest(this).Json;

    public string ContentSha256 => TypeIBaselineContentIdentity.Manifest(this).Digest;

    public static TypeIBaselinePreview Create(Uuid tenantId, Uuid programId,
        Uuid examinationEngagementId, IReadOnlyList<TypeIBaselineSourceReference> sources,
        IReadOnlyList<string>? blockers = null, IReadOnlyList<string>? scopeDifferences = null,
        IReadOnlyList<TypeIBaselineSourceCoverage>? sourceCoverage = null) =>
        new(tenantId, programId, examinationEngagementId,
            Array.AsReadOnly(sources?.ToArray() ?? []),
            Array.AsReadOnly(blockers?.ToArray() ?? []),
            Array.AsReadOnly(scopeDifferences?.ToArray() ?? []))
        {
            SourceCoverage = Array.AsReadOnly(sourceCoverage?.ToArray() ?? []),
        };
}
