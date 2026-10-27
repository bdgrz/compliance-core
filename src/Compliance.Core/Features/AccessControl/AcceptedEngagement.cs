using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One accepted engagement as authority reads it: its current view, its current accepted snapshot and the staff
///     members a directory receipt denies. Whether it still grants professional access is decided against the
///     current service history, rules and directory by <see cref="EngagementProfessionalAccess" />.
/// </summary>
public sealed record AcceptedEngagement(ServiceEngagementView Engagement, ServiceEngagementAcceptanceView Acceptance,
    IReadOnlyList<(Uuid StaffMemberId, Uuid UserId)> DeniedByReceipt);
