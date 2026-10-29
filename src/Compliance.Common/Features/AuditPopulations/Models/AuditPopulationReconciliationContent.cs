namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Claimed source totals; the aggregate verifies these against its exact rows.</summary>
public sealed record AuditPopulationReconciliationContent(
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
    string ReconciliationRationale);
