using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Drives R2-05b, R2-05c, and R2-05d over HTTP against the broker: a frozen procedure, a
///     material deviation, blocked self-review, and the reactor that routes the deviation into an
///     owned finding, in both standalone and split API/worker hosts.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ControlEvaluationE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRouteMaterialDeviationToFindingGivenSubmittedEvaluation(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-evaluations-{Guid.NewGuid():N}";
        using var worker = splitHosts ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var (ownerClient, outsiderClient) = ControlOperationE2ETests.CreateClients(factory,
            splitHosts);
        using var owner = ownerClient;
        using var outsider = outsiderClient;
        var ownerUserId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(owner,
            $"evaluation-owner-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"evaluation-outsider-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await ControlOperationE2ETests.CreateProgramAsync(owner);
        var tenant = Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture);
        var program = Uuid.Parse(programId.ToString(), CultureInfo.InvariantCulture);
        var ownerMemberId = RbacIds.Member(tenant, ownerUserId);
        var controlId = ControlDraft.IdFor(tenant, program, "AC-E2E");
        await ControlOperationE2ETests.SeedApprovedControlAsync(factory, tenant, program,
            controlId, ownerMemberId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-60));
        var programPath = $"/api/v1/tenants/{tenantId}/programs/{programId}";
        var evaluationsPath = $"{programPath}/controls/{controlId}/evaluations";

        // Act
        using var start = await owner.PostAsJsonAsync(evaluationsPath, new
        {
            steps = new[]
            {
                new
                {
                    assertion = "implementation",
                    method = "reperformance",
                    inspected_items = new[]
                    {
                        new { kind = "artifact", reference = "exports/q3.csv", version = "sha256:1" },
                    },
                    expected_condition = "Every leaver was removed.",
                },
            },
        });
        var started = await ControlOperationE2ETests.ReadAsync(start);
        var evaluationId = started.GetProperty("evaluation_id").GetString();
        var stepId = started.GetProperty("steps")[0].GetProperty("step_id").GetString();
        var evaluationPath = $"{evaluationsPath}/{evaluationId}";
        using var record = await owner.PostAsJsonAsync(
            $"{evaluationPath}/steps/{stepId}/results", new
            {
                expected_revision = 1,
                result = "not_met",
                rationale = "Two leavers were still active.",
                inspected_items = new[]
                {
                    new { kind = "artifact", reference = "exports/q3.csv", version = "sha256:1" },
                },
                deviation_classification = "material",
                deviation_description = "Two leavers kept access.",
            });
        using var submit = await owner.PostAsJsonAsync($"{evaluationPath}/submissions", new
        {
            expected_revision = 2,
            conclusions = new[]
            {
                new { assertion = "design", conclusion = "not_tested", rationale = "Out of scope." },
                new
                {
                    assertion = "implementation",
                    conclusion = "ineffective",
                    rationale = "Leavers kept access.",
                },
                new
                {
                    assertion = "evidence_sufficiency",
                    conclusion = "not_tested",
                    rationale = "Out of scope.",
                },
            },
        });
        using var selfReview = await owner.PostAsJsonAsync($"{evaluationPath}/reviews",
            new { expected_revision = 3, decision = "accepted", rationale = "Mine." });
        var findings = await ControlOperationE2ETests.WaitForAsync(owner,
            $"{programPath}/findings", page => page.GetProperty("items").GetArrayLength() > 0);
        var routed = await ControlOperationE2ETests.WaitForAsync(owner, evaluationPath, body =>
            body.GetProperty("deviations")[0].GetProperty("status").GetString() ==
            "routed_to_finding");
        using var outsiderRead = await outsider.GetAsync(evaluationPath);

        // Assert
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal(HttpStatusCode.OK, record.StatusCode);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, selfReview.StatusCode);
        var finding = Assert.Single(findings.GetProperty("items").EnumerateArray());
        Assert.Equal("evaluation_deviation",
            finding.GetProperty("source").GetProperty("kind").GetString());
        Assert.Equal(ownerMemberId.ToString(), finding.GetProperty("owner_member_id").GetString());
        Assert.Equal("ineffective",
            routed.GetProperty("submissions")[0].GetProperty("overall").GetString());
        Assert.True(outsiderRead.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);
    }

    IHost BuildWorker(string applicationName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true)
            .AddWorkers();
        return builder.Build();
    }
}
