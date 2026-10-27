using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

/// <summary>
///     The audience a rule selects from one frozen roster snapshot, plus every person with a
///     current work relationship so removals can be explained as movers or leavers.
/// </summary>
public sealed record RosterAudience(IReadOnlyDictionary<Uuid, RosterMatch> Matching,
    IReadOnlySet<Uuid> Present)
{
    public static RosterAudience Evaluate(WorkforceRosterSnapshotView roster, string kind,
        IReadOnlyList<string> teams)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var names = roster.People.ToDictionary(static person => person.PersonId,
            static person => person.DisplayName);
        var matching = new Dictionary<Uuid, RosterMatch>();
        var present = new HashSet<Uuid>();
        foreach (var relationship in roster.WorkRelationships
                     .OrderBy(static relationship => relationship.SourceWorkerId,
                         StringComparer.Ordinal))
        {
            if (relationship.LifecycleStatus != "ended")
                present.Add(relationship.PersonId);
            if (!PolicyAudience.Matches(kind, teams, relationship))
                continue;
            var name = names.GetValueOrDefault(relationship.PersonId) ?? "Unknown person";
            matching[relationship.PersonId] = matching.TryGetValue(relationship.PersonId,
                out var existing) && existing.StartDate >= relationship.StartDate
                ? existing
                : new RosterMatch(relationship.PersonId, name, relationship.WorkerType,
                    relationship.Department, relationship.StartDate);
        }
        return new RosterAudience(matching, present);
    }
}
