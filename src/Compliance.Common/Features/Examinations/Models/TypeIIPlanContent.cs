using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Approved Type II period intent bound to exact Type I inputs and shared source versions.</summary>
public sealed record TypeIIPlanContent(
    Uuid ExaminationId,
    Uuid SourceSnapshotId,
    string SourceSnapshotSha256,
    Uuid SystemDescriptionVersionId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string AuditFirm,
    IReadOnlyList<string> TrustServiceCategories,
    Uuid TypeIBaselineId,
    Uuid TypeIOutcomeId,
    IReadOnlyList<ExaminationContentReference> CarryForwardInputs,
    IReadOnlyList<TypeIIControlPlanEntry> Controls,
    IReadOnlyList<TypeIICarriedForwardGap> CarriedForwardGaps,
    IReadOnlyList<TypeIIPlanGapAcknowledgement> AcknowledgedGaps,
    IReadOnlyList<TypeIIPlannedChange> RequiredChanges);
