using Cntryl.Portia;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>One person's assignment to one client engagement.</summary>
public sealed record EngagementAssignment(
    Uuid ClientTenantId,
    Uuid EngagementId,
    Uuid FirmStaffMemberId,
    Uuid UserId,
    EngagementPractice Practice);
