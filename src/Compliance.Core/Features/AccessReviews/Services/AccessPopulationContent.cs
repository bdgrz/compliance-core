using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Services;

/// <summary>
///     The canonical v1 rows of an accepted access population: one header row and one row per
///     observed principal, entitlement, membership, and direct assignment, keyed by provider IDs.
/// </summary>
public static class AccessPopulationContent
{
    public const string Kind = "access_population";
    const string HeaderKey = "header";
    const string PrincipalPrefix = "principal/";
    const string EntitlementPrefix = "entitlement/";
    const string MemberPrefix = "group_member/";
    const string AssignmentPrefix = "access_assignment/";

    public static IReadOnlyList<PopulationRow> Rows(AccessPopulationHeader header,
        AccessPopulationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(facts);
        var json = ComplianceCoreJsonContext.Default;
        var rows = new List<PopulationRow>
        {
            new(HeaderKey, JsonSerializer.SerializeToElement(header, json.AccessPopulationHeader)),
        };
        rows.AddRange(facts.Principals.Select(principal => new PopulationRow(
            PrincipalPrefix + principal.ProviderSubjectId,
            JsonSerializer.SerializeToElement(principal, json.AccessPrincipalFact))));
        rows.AddRange(facts.Entitlements.Select(entitlement => new PopulationRow(
            EntitlementPrefix + entitlement.ProviderEntitlementId,
            JsonSerializer.SerializeToElement(entitlement, json.AccessEntitlementFact))));
        rows.AddRange(facts.GroupMembers.Select(member => new PopulationRow(
            MemberPrefix + Pair(member.GroupProviderSubjectId, member.MemberProviderSubjectId),
            JsonSerializer.SerializeToElement(member, json.AccessGroupMemberFact))));
        rows.AddRange(facts.Assignments.Select(assignment => new PopulationRow(
            AssignmentPrefix + Pair(assignment.PrincipalProviderSubjectId,
                assignment.ProviderEntitlementId),
            JsonSerializer.SerializeToElement(assignment, json.AccessAssignmentFact))));
        SnapshotRows.Sort(rows);
        return rows;
    }

    public static (AccessPopulationHeader Header, AccessPopulationFacts Facts) Read(
        PopulationSnapshot snapshot) => Read((snapshot ?? throw new ArgumentNullException(nameof(snapshot))).Rows);

    /// <summary>Reads the header and complete facts from verified rows, inline or chunked.</summary>
    public static (AccessPopulationHeader Header, AccessPopulationFacts Facts) Read(
        IEnumerable<PopulationRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var json = ComplianceCoreJsonContext.Default;
        AccessPopulationHeader? header = null;
        var principals = new List<AccessPrincipalFact>();
        var entitlements = new List<AccessEntitlementFact>();
        var members = new List<AccessGroupMemberFact>();
        var assignments = new List<AccessAssignmentFact>();
        foreach (var row in rows)
        {
            if (row.Key == HeaderKey)
                header = row.Content.Deserialize(json.AccessPopulationHeader);
            else if (row.Key.StartsWith(PrincipalPrefix, StringComparison.Ordinal))
                principals.Add(row.Content.Deserialize(json.AccessPrincipalFact)!);
            else if (row.Key.StartsWith(EntitlementPrefix, StringComparison.Ordinal))
                entitlements.Add(row.Content.Deserialize(json.AccessEntitlementFact)!);
            else if (row.Key.StartsWith(MemberPrefix, StringComparison.Ordinal))
                members.Add(row.Content.Deserialize(json.AccessGroupMemberFact)!);
            else if (row.Key.StartsWith(AssignmentPrefix, StringComparison.Ordinal))
                assignments.Add(row.Content.Deserialize(json.AccessAssignmentFact)!);
            else
                throw new SnapshotContentException("The population snapshot holds an unknown row kind.");
        }
        return (header ?? throw new SnapshotContentException(
            "The population snapshot has no header."), new AccessPopulationFacts(principals,
            entitlements, members, assignments));
    }

    /// <summary>A collision-free key for an ordered pair of provider IDs.</summary>
    static string Pair(string left, string right) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(left + "\0" + right)));
}
