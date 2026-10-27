using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Definition, source facts, reconciliation, and immutable population versions.</summary>
public sealed record AuditPopulationView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid AuditPopulationId,
    Uuid AuditPopulationDefinitionId,
    Uuid TypeIIPeriodId,
    Uuid ExaminationId,
    string Status,
    long Revision,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int CurrentVersionNumber,
    string? GenerationKey,
    IReadOnlyList<AuditPopulationDefinitionVersionView> DefinitionVersions,
    TypeIIPeriodCloseSnapshotView CloseSnapshot,
    IReadOnlyList<AuditPopulationSourceSnapshotView> SourceSnapshots,
    IReadOnlyList<AuditPopulationRowView> Rows,
    AuditPopulationReconciliationView? Reconciliation,
    IReadOnlyList<AuditPopulationVersionView> Versions,
    IReadOnlyList<AuditPopulationAmendmentView> Amendments,
    IReadOnlyList<AuditPopulationExportView> Exports,
    bool SampleEligible,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt)
{
    public AuditPopulationDefinitionVersionView? CurrentDefinition => DefinitionVersions.Count == 0
        ? null : DefinitionVersions[^1];

    public AuditPopulationVersionView? Current => Versions.Count == 0 ? null : Versions[^1];
}
