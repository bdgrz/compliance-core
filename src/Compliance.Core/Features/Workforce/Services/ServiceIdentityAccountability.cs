using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>Explains why a non-human identity is unowned work (M0-D06).</summary>
public static class ServiceIdentityAccountability
{
    public static IReadOnlyList<string> Evaluate(string lifecycleStatus, DateOnly reviewBy,
        DateOnly today, string ownerKind, IReadOnlyCollection<string> ownerRelationshipStatuses,
        bool ownerTeamActive)
    {
        ArgumentNullException.ThrowIfNull(ownerRelationshipStatuses);
        if (lifecycleStatus == "retired")
            return [];
        var reasons = new List<string>();
        if (ownerKind == "person")
        {
            if (ownerRelationshipStatuses.Count == 0)
                reasons.Add("owner_not_on_roster");
            else if (ownerRelationshipStatuses.All(static status => status == "ended"))
                reasons.Add("owner_relationship_ended");
        }
        else if (!ownerTeamActive)
            reasons.Add("owner_team_deleted");
        if (reviewBy < today)
            reasons.Add("review_expired");
        return reasons;
    }

    /// <summary>A non-retired identity is expired from its expiry date onward.</summary>
    public static bool IsExpired(string lifecycleStatus, DateOnly? expiresOn, DateOnly today) =>
        lifecycleStatus != "retired" && expiresOn is { } expiry && expiry <= today;
}
