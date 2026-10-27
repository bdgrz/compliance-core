using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Externally supplied examination result, report provenance, and exact package binding.</summary>
public sealed record AuditOutcomeView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid AuditOutcomeId,
    AuditOutcomeContent Content,
    string Status,
    long Revision,
    Uuid RecordedByMemberId,
    string RecordedByDisplay,
    DateTimeOffset RecordedAt,
    DateTimeOffset UpdatedAt,
    string ContentSha256);
