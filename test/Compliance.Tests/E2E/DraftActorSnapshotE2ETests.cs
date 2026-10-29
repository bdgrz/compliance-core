using Bdgrz.Compliance.Tests.Testing;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
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
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class DraftActorSnapshotE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    sealed record Draft(string Collection, string Current, string Id, string IdKey,
        string StreamArea, string Tool, string HistoryTool);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveThreeDraftActorHistoriesGivenChangedDisplay(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-draft-actor-{Guid.NewGuid():N}";
        IHost? worker = null;
        if (splitHosts)
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
            worker = builder.Build();
            await worker.StartAsync();
        }
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var previousMode = TestHostMode.Current;
            HttpClient owner;
            try
            {
                TestHostMode.Set(splitHosts ? "api" : "standalone");
                owner = factory.CreateClient();
            }
            finally
            {
                TestHostMode.Set(previousMode);
            }
            using (owner)
            {
                var firstEmail = $"draft-actor-first-{Guid.NewGuid():N}@example.com";
                var secondEmail = $"draft-actor-second-{Guid.NewGuid():N}@example.com";
                var userId = await TenantInvitationE2ETests.LoginAsync(owner, firstEmail);
                var (tenantId, programId) = await CreateProgramAsync(owner);
                var memberId = RbacIds.Member(
                    Uuid.Parse(tenantId, CultureInfo.InvariantCulture),
                    Uuid.Parse(userId, CultureInfo.InvariantCulture));
                var serviceId = await CreateServiceAsync(owner, tenantId, programId);
                var drafts = await CreateDraftsAsync(owner, tenantId, programId, serviceId);
                foreach (var draft in drafts)
                    AssertActor((await WaitForCurrentAsync(owner, draft, 1))
                        .GetProperty("last_changed_by"), memberId, firstEmail);

                // Act: a linked developer identity changes the session display but keeps
                // the same platform user and tenant member.
                Assert.Equal(userId, await TenantInvitationE2ETests.LoginAsync(owner, secondEmail));
                using (var controlRevision = await owner.PutAsJsonAsync(drafts[0].Current, new
                {
                    expected_revision = 1,
                    command_id = Uuid.CreateVersion4(),
                    content = ControlContent("Monthly access review"),
                }))
                    Assert.Equal(HttpStatusCode.NoContent, controlRevision.StatusCode);
                using (var commitmentRevision = await CommitmentHttpRequests.ReviseAsync(owner, drafts[1].Current,
                           new
                           {
                               expected_revision = 1,
                               statement = "Monthly service commitment",
                               context = "Draft context",
                               source_reference = "Contract section 5",
                           }))
                    Assert.Equal(HttpStatusCode.OK, commitmentRevision.StatusCode);
                using (var riskRevision = await RiskDraftHttpRequests.ReviseAsync(owner, drafts[2].Current, new
                {
                    expected_revision = 1,
                    title = "Provider outage",
                    scenario = "Extended provider outage",
                    potential_effect = "Requests cannot be processed",
                    source_note = "Management note",
                }))
                    Assert.Equal(HttpStatusCode.OK, riskRevision.StatusCode);

                // Assert: current and immutable history use saved IDs and displays.
                await using var mcp = await McpScenario.ConnectAsync(owner,
                    new Uri(owner.BaseAddress!, "/mcp"));
                foreach (var draft in drafts)
                {
                    var current = await WaitForCurrentAsync(owner, draft, 2);
                    AssertActor(current.GetProperty("last_changed_by"), memberId, secondEmail);
                    using var listResponse = await owner.GetAsync(draft.Collection);
                    Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
                    var listed = (await ReadJsonAsync(listResponse)).GetProperty("items");
                    AssertActor(Assert.Single(listed.EnumerateArray(),
                        item => item.GetProperty(draft.IdKey).GetString() == draft.Id)
                        .GetProperty("last_changed_by"), memberId, secondEmail);
                    var history = await WaitForHistoryAsync(owner, draft.Current, 2);
                    AssertActor(history[0].GetProperty("actor"), memberId, firstEmail);
                    AssertActor(history[1].GetProperty("actor"), memberId, secondEmail);
                    for (var revision = 1; revision <= 2; revision++)
                    {
                        using var exactResponse = await owner.GetAsync(
                            $"{draft.Current}/revisions/{revision}");
                        Assert.Equal(HttpStatusCode.OK, exactResponse.StatusCode);
                        AssertActor((await ReadJsonAsync(exactResponse)).GetProperty("actor"),
                            memberId, revision == 1 ? firstEmail : secondEmail);
                    }

                    var input = new Dictionary<string, object?>
                    {
                        ["tenant_id"] = tenantId,
                        ["program_id"] = programId,
                        [draft.IdKey] = draft.Id,
                    };
                    var mcpCurrent = await ReadMcpResultAsync(mcp, draft.Tool + ".get", input);
                    AssertActor(mcpCurrent.GetProperty("last_changed_by"), memberId, secondEmail);
                    var mcpList = (await ReadMcpResultAsync(mcp, draft.Tool + ".list",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantId,
                            ["program_id"] = programId,
                        })).GetProperty("items");
                    AssertActor(Assert.Single(mcpList.EnumerateArray(),
                        item => item.GetProperty(draft.IdKey).GetString() == draft.Id)
                        .GetProperty("last_changed_by"), memberId, secondEmail);
                    var mcpHistory = (await ReadMcpResultAsync(mcp, draft.HistoryTool, input))
                        .GetProperty("items");
                    Assert.Equal(2, mcpHistory.GetArrayLength());
                    AssertActor(mcpHistory[0].GetProperty("actor"), memberId, firstEmail);
                    AssertActor(mcpHistory[1].GetProperty("actor"), memberId, secondEmail);
                    var exactInput = new Dictionary<string, object?>(input) { ["revision"] = 1 };
                    var mcpExact = await ReadMcpResultAsync(mcp,
                        draft.Tool + ".revision.get", exactInput);
                    AssertActor(mcpExact.GetProperty("actor"), memberId, firstEmail);

                    var stored = new List<DomainEvent>();
                    await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                                       .ReadAsync(new EventStreamAddress(tenantId,
                                               draft.StreamArea, draft.Id), 0,
                                           CancellationToken.None))
                        stored.Add(record.Event);
                    Assert.Equal(2, stored.Count);
                    Assert.Equal(ActorReference.ForMember(memberId, firstEmail),
                        StoredActor(stored[0]));
                    Assert.Equal(ActorReference.ForMember(memberId, secondEmail),
                        StoredActor(stored[1]));
                }
            }
        }
        finally
        {
            if (worker is not null)
            {
                await worker.StopAsync();
                worker.Dispose();
            }
        }
    }

    static async Task<(string TenantId, string ProgramId)> CreateProgramAsync(HttpClient owner)
    {
        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Draft actor tenant",
            slug = $"draft-actor-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenantId = (await ReadJsonAsync(tenantResponse)).GetProperty("tenant_id")
            .GetString()!;
        var path = $"/api/v1/tenants/{tenantId}/programs";
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, tenantId);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PutAsJsonAsync(path + $"/{Uuid.CreateVersion4()}", new
            {
                name = "Draft actor program",
                plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Advisor",
                    audit_firm = (string?)null,
                },
            });
            if (response.StatusCode == HttpStatusCode.OK)
                return (tenantId, (await ReadJsonAsync(response)).GetProperty("program_id")
                    .GetString()!);
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program management did not become available.");
    }

    static async Task<string> CreateServiceAsync(HttpClient owner, string tenantId,
        string programId)
    {
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/client-services";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PutAsJsonAsync(path + $"/{Uuid.CreateVersion4()}", new
            {
                name = "Payroll",
                purpose = "Process payroll",
                owner_reference = "Operations",
            });
            if (response.StatusCode == HttpStatusCode.OK)
                return (await ReadJsonAsync(response)).GetProperty("service_id").GetString()!;
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Service management did not become available.");
    }

    static async Task<Draft[]> CreateDraftsAsync(HttpClient owner, string tenantId,
        string programId, string serviceId)
    {
        var prefix = $"/api/v1/tenants/{tenantId}/programs/{programId}";
        var controls = prefix + "/controls";
        var commitments = prefix + "/commitment-drafts";
        var risks = prefix + "/risks";
        using var controlResponse = await owner.PostAsJsonAsync(controls, new
        {
            identifier = "AC-01",
            content = ControlContent("Quarterly access review"),
        });
        using var commitmentResponse = await CommitmentHttpRequests.CreateAsync(owner, commitments, new
        {
            service_id = serviceId,
            kind = "service_commitment",
            identifier = "SC-01",
            statement = "Quarterly service commitment",
            context = "Draft context",
            source_reference = "Contract section 4",
        });
        using var riskResponse = await RiskDraftHttpRequests.CreateAsync(owner, risks, new
        {
            identifier = "R-01",
            title = "Provider outage",
            scenario = "Provider becomes unavailable",
            potential_effect = "Requests cannot be processed",
            source_note = "Management note",
        });
        foreach (var response in new[] { controlResponse, commitmentResponse, riskResponse })
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var controlId = (await ReadJsonAsync(controlResponse)).GetProperty("control_id")
            .GetString()!;
        var commitmentId = (await ReadJsonAsync(commitmentResponse)).GetProperty("draft_id")
            .GetString()!;
        var riskId = (await ReadJsonAsync(riskResponse)).GetProperty("risk_id")
            .GetString()!;
        return
        [
            new Draft(controls, $"{controls}/{controlId}/draft", controlId, "control_id",
                "controls", "bdgrz.control.draft", "bdgrz.control.draft.revisions.list"),
            new Draft(commitments, $"{commitments}/{commitmentId}", commitmentId, "draft_id",
                "commitment-drafts", "bdgrz.commitment.draft", "bdgrz.commitment.draft.revision.list"),
            new Draft(risks, $"{risks}/{riskId}/draft", riskId, "risk_id",
                "risks", "bdgrz.risk.draft", "bdgrz.risk.draft.revisions.list"),
        ];
    }

    static object ControlContent(string title) => new
    {
        title,
        objective = "Review access",
        description = "Management reviews access",
        implementation_narrative = "The owner reviews the access list quarterly.",
        expected_evidence_descriptions = new[] { "Dated review record" },
    };

    static async Task<JsonElement> WaitForCurrentAsync(HttpClient owner, Draft draft,
        long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(draft.Current);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var current = await ReadJsonAsync(response);
                if (current.GetProperty("revision").GetInt64() == revision)
                    return current;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"{draft.Tool} revision {revision} did not project.");
    }

    static async Task<JsonElement[]> WaitForHistoryAsync(HttpClient owner, string currentPath,
        int count)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync($"{currentPath}/revisions");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var items = (await ReadJsonAsync(response)).GetProperty("items")
                    .EnumerateArray().ToArray();
                if (items.Length == count)
                    return items;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"{currentPath} history did not reach {count} revisions.");
    }

    static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    static async Task<JsonElement> ReadMcpResultAsync(McpScenario mcp, string tool,
        Dictionary<string, object?> input)
    {
        var call = await mcp.When(tool, input).ExpectSuccess();
        return Assert.IsType<JsonElement>(call.StructuredJson).GetProperty("result");
    }

    static ActorReference? StoredActor(DomainEvent ev) => ev switch
    {
        ControlDraftCreated created => created.StoredActor,
        ControlDraftRevised revised => revised.StoredActor,
        CommitmentDraftCreated created => created.StoredActor,
        CommitmentDraftRevised revised => revised.StoredActor,
        RiskDraftCreated created => created.StoredActor,
        RiskDraftRevised revised => revised.StoredActor,
        _ => throw new InvalidOperationException($"Unexpected event {ev.GetType().Name}."),
    };

    static void AssertActor(JsonElement actor, Uuid memberId, string display)
    {
        Assert.Equal("member", actor.GetProperty("kind").GetString());
        Assert.Equal(memberId.ToString(), actor.GetProperty("id").GetString());
        Assert.Equal(display, actor.GetProperty("display").GetString());
    }
}
