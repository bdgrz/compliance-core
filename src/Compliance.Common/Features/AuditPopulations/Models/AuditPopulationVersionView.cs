using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>An immutable, sample-bindable population snapshot.</summary>
public sealed record AuditPopulationVersionView(
    int Version,
    int DefinitionVersion,
    IReadOnlyList<AuditPopulationSourceSnapshotView> SourceSnapshots,
    IReadOnlyList<AuditPopulationRowView> Rows,
    AuditPopulationReconciliationView Reconciliation,
    Uuid CloseSnapshotId,
    string CloseSnapshotSha256,
    string ContentSha256,
    bool SampleEligible,
    Uuid FrozenByMemberId,
    string FrozenByDisplay,
    DateTimeOffset FrozenAt,
    string FreezeRationale);
