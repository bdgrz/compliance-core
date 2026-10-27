using System.Globalization;
using System.Net;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Projections;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

[Trait("Category", "WebIntegration")]
public sealed class AuditorRequestScopedReadHttpTests
{
    [Theory]
    [InlineData("requests")]
    [InlineData("responses")]
    [InlineData("selection")]
    public async Task ShouldReturnCompleteScopedReadGivenActualNativeSourceAndCaughtUpDiscovery(string route)
    {
        // Arrange
        await using var fixture = await HttpFixture.CreateAsync();
        var native = fixture.Native;
        var response = Uuid.CreateVersion4();
        Assert.True((await native.DraftAsync(fixture.RequestId, response)).IsSuccess);
        Assert.Equal(Bdgrz.Compliance.Features.Processes.Processes.ProcessState.Completed, (await native.MaterializeAsync(response)).State);
        Assert.True((await native.SubmitAsync(fixture.RequestId, response)).IsSuccess);
        var population = await native.SeedFrozenPopulationAsync();
        var selection = Uuid.CreateVersion4();
        await fixture.SelectAsync(selection, population.PopulationId, population.IncludedRowId);
        Assert.Equal(Bdgrz.Compliance.Features.Processes.Processes.ProcessState.Completed, (await native.MaterializeAsync(selection)).State);
        await fixture.ProjectAsync();
        var path = route switch
        {
            "requests" => fixture.Prefix,
            "responses" => $"{fixture.Prefix}/{fixture.RequestId}/responses",
            _ => $"{fixture.ProgramPrefix}/sample-selections/{selection}",
        };

        // Act
        using var result = await fixture.Client.GetAsync(path);
        var json = await result.Content.ReadAsStringAsync();

        // Assert
        Assert.True(result.StatusCode == HttpStatusCode.OK, json);
        if (route == "requests")
        {
            var page = JsonSerializer.Deserialize(json, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Page<AuditorRequestSummaryView>>)ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(Page<AuditorRequestSummaryView>))!)!;
            var item = Assert.Single(page.Items);
            Assert.Equal(fixture.RequestId, item.AuditorRequestId);
            Assert.Equal("External request wording", item.Name);
            Assert.Equal("submitted", item.Status);
        }
        else if (route == "responses")
        {
            var page = JsonSerializer.Deserialize(json, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Page<AuditorRequestResponseView>>)ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(Page<AuditorRequestResponseView>))!)!;
            var item = Assert.Single(page.Items);
            Assert.Equal(response, item.AuditorRequestResponseId);
            Assert.Equal(fixture.RequestId, item.AuditorRequestId);
            Assert.Equal("submitted", item.Status);
            Assert.Null(item.ArtifactId);
        }
        else
        {
            var item = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.SampleSelectionView)!;
            Assert.Equal(selection, item.SampleSelectionId);
            Assert.Equal(fixture.RequestId, item.AuditorRequestId);
            Assert.Equal(population.ContentSha256, item.PopulationContentSha256);
            Assert.Equal(population.IncludedRowId, Assert.Single(item.SelectedRowIds));
        }
    }

    [Fact]
    public async Task ShouldRequireCaughtUpSelectionLocatorAndActualMaterializationGivenHttpRead()
    {
        // Arrange
        await using var fixture = await HttpFixture.CreateAsync();
        var population = await fixture.Native.SeedFrozenPopulationAsync();
        var selection = Uuid.CreateVersion4();
        await fixture.SelectAsync(selection, population.PopulationId, population.IncludedRowId);
        var route = $"{fixture.ProgramPrefix}/sample-selections/{selection}";

        // Act
        using var behind = await fixture.Client.GetAsync(route);
        await fixture.ProjectAsync();
        using var pending = await fixture.Client.GetAsync(route);
        await fixture.Native.MaterializeAsync(selection);
        using var complete = await fixture.Client.GetAsync(route);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, behind.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, pending.StatusCode);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("requests")]
    [InlineData("responses")]
    [InlineData("selection")]
    public async Task ShouldRequireCurrentClientPermissionGivenExistingScopedRead(string kind)
    {
        // Arrange
        await using var fixture = await HttpFixture.CreateAsync();
        await fixture.ProjectAsync();
        var path = kind switch
        {
            "request" => $"{fixture.Prefix}/{fixture.RequestId}",
            "requests" => fixture.Prefix,
            "responses" => $"{fixture.Prefix}/{fixture.RequestId}/responses",
            _ => $"{fixture.ProgramPrefix}/sample-selections/{Uuid.CreateVersion4()}",
        };
        fixture.Permissions.Allowed = false;
        using var anonymous = fixture.Factory.CreateClient();

        // Act
        using var denied = await fixture.Client.GetAsync(path);
        using var unauthenticated = await anonymous.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("firm_staff")]
    public async Task ShouldPreserveClientOnlyDisclosureGivenProfessionalOrGuestMembership(string affiliation)
    {
        // Arrange
        await using var fixture = await HttpFixture.CreateAsync(affiliation);
        var route = $"{fixture.Prefix}/{fixture.RequestId}";

        // Act
        using var result = await fixture.Client.GetAsync(route);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task ShouldRefuseAnotherExistingProgramAndMalformedRequestIdentityGivenHttpRead()
    {
        // Arrange
        await using var fixture = await HttpFixture.CreateAsync();
        var otherProgram = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(otherProgram);
        var other = $"/api/v1/tenants/{fixture.Native.TenantId}/programs/{otherProgram}/auditor-requests/{fixture.RequestId}";

        // Act
        using var wrongScope = await fixture.Client.GetAsync(other);
        using var malformed = await fixture.Client.GetAsync($"{fixture.Prefix}/not-a-uuid");
        using var empty = await fixture.Client.GetAsync($"{fixture.Prefix}/{Uuid.Empty}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, wrongScope.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task ShouldRefuseForeignContinuationGivenActualScopedRequestPages()
    {
        // Arrange
        await using var fixture = await HttpFixture.CreateAsync();
        var second = fixture.Native.NewRecordCommand() with { Wording = "Second request" };
        Assert.True((await fixture.Native.Handler<RecordAuditorRequestHandler>()
            .HandleAsync(fixture.Native.Context(second), CancellationToken.None)).IsSuccess);
        await fixture.ProjectAsync();
        using var first = await fixture.Client.GetAsync(fixture.Prefix + "?limit=1");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var page = JsonSerializer.Deserialize(await first.Content.ReadAsStringAsync(),
            (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Page<AuditorRequestSummaryView>>)ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(Page<AuditorRequestSummaryView>))!)!;
        Assert.NotNull(page.NextCursor);
        var otherProgram = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(otherProgram);
        var other = $"/api/v1/tenants/{fixture.Native.TenantId}/programs/{otherProgram}/auditor-requests";

        // Act
        using var next = await fixture.Client.GetAsync(fixture.Prefix + "?limit=1&cursor=" + Uri.EscapeDataString(page.NextCursor));
        using var wrong = await fixture.Client.GetAsync(other + "?limit=1&cursor=" + Uri.EscapeDataString(page.NextCursor));

        // Assert
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        var last = JsonSerializer.Deserialize(await next.Content.ReadAsStringAsync(),
            (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Page<AuditorRequestSummaryView>>)ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(Page<AuditorRequestSummaryView>))!)!;
        Assert.NotEqual(Assert.Single(page.Items).AuditorRequestId, Assert.Single(last.Items).AuditorRequestId);
        Assert.Null(last.NextCursor);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldUseCanonicalRouteIdentityGivenMisleadingAuditorReadQuery(bool responses)
    {
        // Arrange
        await using var fixture = await HttpFixture.CreateAsync();
        var route = $"{fixture.Prefix}/{fixture.RequestId}" + (responses ? "/responses" : string.Empty);
        var query = $"?tenant_id={Uuid.CreateVersion4()}&program_id={Uuid.CreateVersion4()}&auditor_request_id={Uuid.CreateVersion4()}";

        // Act
        using var result = await fixture.Client.GetAsync(route + query);
        var json = await result.Content.ReadAsStringAsync();

        // Assert
        Assert.True(result.StatusCode == HttpStatusCode.OK, json);
        using var body = JsonDocument.Parse(json);
        if (!responses)
        {
            Assert.Equal(fixture.Native.TenantId.ToString(), body.RootElement.GetProperty("tenant_id").GetString());
            Assert.Equal(fixture.Native.ProgramId.ToString(), body.RootElement.GetProperty("program_id").GetString());
            Assert.Equal(fixture.RequestId.ToString(), body.RootElement.GetProperty("auditor_request_id").GetString());
        }
        else
            Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
    }

    sealed class HttpFixture : IAsyncDisposable
    {
        public AuditorRequestFixture Native { get; } = new();
        public MutablePermissions Permissions { get; } = new();
        public WebApplicationFactory<global::Program> Factory { get; }
        public HttpClient Client { get; }
        public Uuid RequestId { get; private set; }
        public string ProgramPrefix => $"/api/v1/tenants/{Native.TenantId}/programs/{Native.ProgramId}";
        public string Prefix => ProgramPrefix + "/auditor-requests";

        HttpFixture(string affiliation)
        {
            Factory = ProviderHttpMcpTests.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.RemoveAll<IImmutableContentStore>();
                services.AddSingleton<IEventStore>(Native.Events);
                services.AddSingleton<IDomainEventReader>(Native.Events);
                services.AddSingleton(Native.Content);
                services.RemoveAll<IPermissionAuthorizer>();
                services.AddSingleton<IPermissionAuthorizer>(Permissions);
                services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(Permissions));
                services.RemoveAll<ITenantMembershipDirectoryReader>();
                services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true, affiliation));
            }));
            Client = Factory.CreateClient();
        }

        public static async Task<HttpFixture> CreateAsync(string affiliation = "client_personnel")
        {
            var fixture = new HttpFixture(affiliation);
            fixture.RequestId = await fixture.Native.SeedRequestAsync();
            await fixture.SeedProgramAsync(fixture.Native.ProgramId);
            var user = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(fixture.Client, "auditor-scoped-reader@example.test"), CultureInfo.InvariantCulture);
            await fixture.Native.SeedMemberAsync(user);
            return fixture;
        }

        public async Task SeedProgramAsync(Uuid programId)
        {
            var program = new ProgramRules(Native.TenantId, programId);
            Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), Native.MemberId,
                "Program author", AuditorRequestFixture.Now));
            await ProgramSeeds.WriteAsync(Native.Reader, Native.Writer, Native.Events, Native.Content, program);
        }

        public async Task SelectAsync(Uuid selection, Uuid population, Uuid row)
        {
            var request = await Native.RequestAsync(RequestId);
            var command = new RecordSampleSelection(Native.TenantId, Native.ProgramId, Native.ExaminationId, RequestId,
                request.Revision, selection, population, 1, [row], "random", "external_email", "sample-source");
            var admitted = await Native.Handler<RecordSampleSelectionHandler>().HandleAsync(Native.Context(command), CancellationToken.None);
            Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        }

        public async Task ProjectAsync()
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var projection = scope.ServiceProvider.GetRequiredService<IAuditorRequestDirectoryProjection>();
            var directory = scope.ServiceProvider.GetRequiredService<IAuditorRequestDirectoryReader>();
            var identity = new CheckpointIdentity(FitzAuditorRequestDirectory.ProjectorName,
                EventStreamPattern.ForPattern(Native.TenantId.ToString(), AuditorRequest.Area));
            var checkpoint = await directory.LoadCheckpointAsync(Native.TenantId);
            await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity, checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in Native.Events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
            {
                if (record.Event is AuditorRequestRecorded created)
                    await projection.ApplyAsync(created);
                if (record.Event is AuditorSampleSelectionAdmitted selection)
                    await projection.ApplyAsync(selection);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            await Native.DisposeAsync();
        }
    }

    sealed class MutablePermissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
}
