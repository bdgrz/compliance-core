using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Current complete-close readiness at one verified period and source receipt cut; this report is not an admission.</summary>
public sealed record ObservationPeriodClosePreview(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId,
    long Revision, bool Ready, int ExpectedOccurrenceCount, int ActualOccurrenceCount,
    IReadOnlyList<Uuid> MissingWorkIds, IReadOnlyList<Uuid> DuplicateOccurrenceIds,
    IReadOnlyList<Uuid> UnresolvedMaterialItemIds, IReadOnlyList<string> Blockers,
    IReadOnlyList<string> SourceDifferences, DateTimeOffset CalculatedAt);
