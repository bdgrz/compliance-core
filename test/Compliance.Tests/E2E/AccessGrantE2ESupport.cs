using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.E2E;

static class AccessGrantE2ESupport
{
    public static Task<Uuid> IssueFounderOrganizationGrantAsync(HttpClient client, Guid tenantId) =>
        IssueFounderOrganizationGrantAsync(client, tenantId.ToString());

    public static Task<Uuid> IssueFounderOrganizationGrantAsync(HttpClient client, string tenantId) =>
        IssueFounderOrganizationGrantAsync(client, Uuid.Parse(tenantId, CultureInfo.InvariantCulture));

    public static Task<Uuid> IssueFounderOrganizationGrantForProgramPathAsync(HttpClient client,
        string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var tenantIndex = Array.IndexOf(segments, "tenants");
        Assert.True(tenantIndex >= 0 && tenantIndex + 2 < segments.Length &&
            segments[tenantIndex + 2] == "programs", "The path must identify a tenant's programs.");
        return IssueFounderOrganizationGrantAsync(client, segments[tenantIndex + 1]);
    }

    public static async Task<Uuid> IssueFounderOrganizationGrantAsync(HttpClient client, Uuid tenantId)
    {
        using var session = await client.GetAsync("/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        using var document = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
        var userId = Uuid.Parse(document.RootElement.GetProperty("id").GetString()!,
            CultureInfo.InvariantCulture);
        return await IssueOrganizationGrantAsync(client, tenantId, "member",
            RbacIds.Member(tenantId, userId), BuiltInRbac.TenantAdministrationRoleId(tenantId),
            "founder-setup");
    }

    public static Task<Uuid> IssuePowerUserTeamOrganizationGrantAsync(HttpClient client,
        Uuid tenantId) => IssueOrganizationGrantAsync(client, tenantId, "team",
        BuiltInRbac.PowerUsersTeamId(tenantId), BuiltInRbac.ComplianceManagementRoleId(tenantId),
        "power-user-team-setup");

    static async Task<Uuid> IssueOrganizationGrantAsync(HttpClient client, Uuid tenantId,
        string principalKind, Uuid principalId, Uuid roleId, string sourceId)
    {
        var grantId = Uuid.CreateVersion4();
        var path = $"/api/v1/tenants/{tenantId}/access-grants/{grantId}";
        var listPath = $"/api/v1/tenants/{tenantId}/access-grants";
        using (var existing = await client.GetAsync(listPath))
        {
            if (existing.StatusCode == HttpStatusCode.OK)
            {
                using var grants = JsonDocument.Parse(await existing.Content.ReadAsStringAsync());
                foreach (var grant in grants.RootElement.GetProperty("grants").EnumerateArray())
                {
                    var terms = grant.GetProperty("terms");
                    if (terms.GetProperty("source").GetProperty("id").GetString() == sourceId &&
                        terms.GetProperty("role_id").GetString() == roleId.ToString() &&
                        terms.GetProperty("principal").GetProperty("kind").GetString() ==
                        principalKind &&
                        terms.GetProperty("principal").GetProperty("id").GetString() ==
                        principalId.ToString() &&
                        (!grant.TryGetProperty("revoked_at", out var revoked) ||
                         revoked.ValueKind == JsonValueKind.Null))
                        return Uuid.Parse(grant.GetProperty("grant_id").GetString()!,
                            CultureInfo.InvariantCulture);
                }
            }
        }
        var proposal = new
        {
            principal = new { kind = principalKind, id = principalId.ToString() },
            role_id = roleId.ToString(),
            scope = new { kind = "organization", id = tenantId.ToString() },
            source = new { kind = "manual", id = sourceId },
            effective_from = DateTimeOffset.UtcNow.AddMinutes(-1),
            effective_until = (DateTimeOffset?)null,
        };

        // After a broker restore, split-host workers replay every per-tenant projector before permissions
        // catch up, so allow well beyond ordinary lag.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? lastResponse = null;
        var succeeded = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var issued = await client.PostAsJsonAsync(path, new { proposal });
            if (issued.IsSuccessStatusCode)
            {
                succeeded = true;
                break;
            }
            lastResponse = $"{(int)issued.StatusCode} {await issued.Content.ReadAsStringAsync()}";
            Assert.True(issued.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                lastResponse);
            await Task.Delay(250);
        }

        Assert.True(succeeded,
            $"The {principalKind} could not receive organization grant {grantId} in tenant {tenantId}: " +
            lastResponse);

        var projectionDeadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? lastProjection = null;
        while (DateTimeOffset.UtcNow < projectionDeadline)
        {
            using var listed = await client.GetAsync(listPath);
            if (listed.StatusCode == HttpStatusCode.OK)
            {
                using var grants = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
                var items = grants.RootElement.GetProperty("grants").EnumerateArray();
                if (items.Any(grant =>
                        grant.GetProperty("grant_id").GetString() == grantId.ToString()))
                    return grantId;
                lastProjection = $"200 OK; {items.Count()} grant(s) projected";
            }
            else
                lastProjection = $"{(int)listed.StatusCode} {await listed.Content.ReadAsStringAsync()}";
            await Task.Delay(250);
        }

        throw new TimeoutException($"{principalKind} organization grant {grantId} in tenant {tenantId} " +
            $"did not reach the projection; last GET: {lastProjection}");
    }
}
