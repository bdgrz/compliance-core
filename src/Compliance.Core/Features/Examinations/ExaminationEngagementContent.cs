using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Mutable Type I engagement setup terms, before its baseline is frozen.</summary>
public sealed record ExaminationEngagementContent(
    string EngagementType,
    string AuditFirm,
    DateOnly PeriodStart,
    DateOnly? PeriodEnd,
    IReadOnlyList<string> TrustServicesCategories,
    Uuid BoundaryId,
    IReadOnlyList<Uuid> ManagementContactMemberIds);
