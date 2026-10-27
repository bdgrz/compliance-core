using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Type I engagement terms and the exact baseline lifecycle revision.</summary>
public sealed record ExaminationEngagementView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ExaminationEngagementId,
    string EngagementType,
    string AuditFirm,
    DateOnly PeriodStart,
    DateOnly? PeriodEnd,
    IReadOnlyList<string> TrustServicesCategories,
    Uuid BoundaryId,
    IReadOnlyList<Uuid> ManagementContactMemberIds,
    string Status,
    long Revision,
    DateTimeOffset UpdatedAt,
    Uuid ActorMemberId,
    string ActorDisplay);
