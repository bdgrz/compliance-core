using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Immutable management review binding an examination to exact evidence and external outcomes.</summary>
public sealed record ExaminationSignOffView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ExaminationSignOffId,
    ExaminationSignOffContent Content,
    string Status,
    long Revision,
    Uuid SignedOffByMemberId,
    string ApproverRole,
    string ApproverDisplay,
    DateTimeOffset SignedOffAt,
    DateTimeOffset UpdatedAt);
