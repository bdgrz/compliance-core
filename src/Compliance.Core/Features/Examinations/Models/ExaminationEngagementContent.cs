using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Mutable Type I engagement setup terms, before its baseline is frozen.</summary>
public sealed record ExaminationEngagementContent(
    string EngagementType,
    string AuditFirm,
    DateOnly PeriodStart,
    DateOnly? PeriodEnd,
    IReadOnlyList<string> TrustServicesCategories,
    Uuid BoundaryId,
    IReadOnlyList<Uuid> ManagementContactMemberIds);
