using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Current examination setup; baseline and period facts retain their own owners.</summary>
public sealed record ExaminationView(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    string ExaminationType, string Status, long Revision, ExaminationAuditor Auditor,
    ExaminationDates TargetDates, ExaminationDates? ConfirmedDates, string? ConfirmationEvidenceReference,
    Uuid? CeoSignerMemberId, Uuid? CisoSignerMemberId, ExaminationClosure? Closure);
