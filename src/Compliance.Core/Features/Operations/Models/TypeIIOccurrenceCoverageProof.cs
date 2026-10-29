using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>
///     The coverage of one period's recurring occurrences: the canonical ids that were selected, those whose
///     materialization is proven, those selected but not materialized, and the period's expected recurring occurrences
///     that no generation selected. Close requires both missing lists to be empty.
/// </summary>
public sealed record TypeIIOccurrenceCoverageProof(IReadOnlyList<Uuid> Selected, IReadOnlyList<Uuid> Materialized,
    IReadOnlyList<Uuid> NotMaterialized, IReadOnlyList<Uuid> NotSelected)
{
    /// <summary>True only when every expected recurring occurrence is selected and every selected one is materialized.</summary>
    public bool IsComplete => NotMaterialized.Count == 0 && NotSelected.Count == 0;
}
