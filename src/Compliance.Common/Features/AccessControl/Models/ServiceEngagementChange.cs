using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The normalized, accepted business values of one lifecycle command, tagged by its stable command kind. It holds
///     only what that command changed plus the directory staff snapshot it relied on, never history or children.
/// </summary>
public sealed record ServiceEngagementChange(string Kind, ServiceEngagementDraftContent? Content,
    FirmStaffMemberView? Staff, Uuid? StaffMemberId, string? Reason, ActorReference Actor, DateTimeOffset RecordedAt);
