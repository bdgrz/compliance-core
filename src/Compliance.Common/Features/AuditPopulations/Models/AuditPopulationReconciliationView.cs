using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Reconciled row totals and completeness, bound to the exact source snapshot set.</summary>
public sealed record AuditPopulationReconciliationView(
    string Completeness,
    int SourceTotal,
    int IncludedCount,
    int ExcludedCount,
    int DuplicateCount,
    int MissingCount,
    int RejectedCount,
    int UnresolvedCount,
    int CanceledCount,
    int NotApplicableCount,
    int ExceptionBackedCount,
    IReadOnlyList<Uuid> SourceSnapshotIds,
    string ReconciliationRationale,
    string ContentSha256,
    Uuid ReconciledByMemberId,
    string ReconciledByDisplay,
    DateTimeOffset ReconciledAt);
