using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Current scoped period status and dates; discovery ordering is held separately by immutable opening identity.</summary>
public sealed record ObservationPeriodSummaryView(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId,
    Uuid ExaminationId, string Status, long Revision, DateOnly PeriodStart, DateOnly PeriodEnd, DateTimeOffset UpdatedAt);
