using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     Whether the active acceptance of an engagement still grants a staff member professional access. Authority
///     reads the current service history, rules and directory authoritatively: a changed service epoch makes an old
///     acceptance ineligible immediately, and a directory receipt only ever denies.
/// </summary>
public static class EngagementProfessionalAccess
{
    /// <summary>True when the accepted snapshot of <paramref name="accepted" /> grants this staff member access now.</summary>
    public static bool IsEligible(AcceptedEngagement accepted, IReadOnlyCollection<Uuid> currentServiceRecordIds,
        Uuid staffMemberId, Uuid userId, long currentRuleVersion, long currentDirectoryStaffRevision,
        DateTimeOffset effectiveAt)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(currentServiceRecordIds);
        var (engagement, acceptance) = (accepted.Engagement, accepted.Acceptance);
        var day = DateOnly.FromDateTime(effectiveAt.UtcDateTime);
        return acceptance.Status == "active" && acceptance.RecordedAt <= effectiveAt &&
            engagement.Status != "closed" && engagement.RecordedAt <= effectiveAt &&
            day >= engagement.Content.PeriodStart &&
            (engagement.Content.PeriodEnd is null || day <= engagement.Content.PeriodEnd) &&
            acceptance.Rules.Version == currentRuleVersion &&
            !accepted.DeniedByReceipt.Any(denied => denied.StaffMemberId == staffMemberId && denied.UserId == userId) &&
            CompleteFacts(acceptance.CompleteServiceHistory.Select(service => service.ServiceRecordId).ToArray(),
                currentServiceRecordIds) &&
            acceptance.Assignments.Any(staff => staff.IsCurrent && staff.StaffMemberId == staffMemberId &&
                staff.UserId == userId && staff.AssignedAt <= effectiveAt && EffectiveFrom(staff) <= day &&
                staff.DirectoryStaffRevision == currentDirectoryStaffRevision);
    }

    static bool CompleteFacts(Uuid[] accepted, IReadOnlyCollection<Uuid> current) =>
        accepted.Distinct().Count() == accepted.Length && accepted.Length == current.Count &&
        accepted.ToHashSet().SetEquals(current);

    static DateOnly EffectiveFrom(EngagementActualAssignmentView staff) => staff.EffectiveFrom == default
        ? DateOnly.FromDateTime(staff.AssignedAt.UtcDateTime)
        : staff.EffectiveFrom;
}
