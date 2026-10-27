using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Current period lifecycle authority and its exact approved-plan, date and close bindings.</summary>
public sealed record ObservationPeriodView(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId,
    Uuid ExaminationId, Uuid TypeIIPlanId, int TypeIIPlanVersion, string Status, long Revision,
    DateOnly PeriodStart, DateOnly PeriodEnd, int ExpectedOccurrenceCount, Uuid? PendingAmendmentId,
    string? CloseValidationStatus, Uuid? LatestCloseSnapshotId, int? LatestCloseSnapshotVersion,
    DateTimeOffset UpdatedAt);
