using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

[Trait("Category", "WebIntegration")]
public sealed class SystemDescriptionHttpTests
{
    [Fact]
    public async Task ShouldUseCanonicalDescriptionRouteGivenMisleadingQueryIdentities()
    {
        // Arrange
        await using var fixture = new Fixture();
        var descriptionId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync($"/{descriptionId}?system_description_id={Uuid.CreateVersion4()}&" +
            $"tenant_id={Uuid.CreateVersion4()}&program_id={Uuid.CreateVersion4()}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(descriptionId.ToString(), body.RootElement.GetProperty("system_description_id").GetString());
        Assert.Equal(fixture.Tenant.ToString(), body.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(fixture.Program.ToString(), body.RootElement.GetProperty("program_id").GetString());
    }

    [Fact]
    public async Task ShouldReturnActualNativeSummaryGivenProductionScopedListRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var descriptionId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync($"/{descriptionId}/versions?limit=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(descriptionId.ToString(), summary.GetProperty("system_description_id").GetString());
        Assert.Equal("HTTP narrative", summary.GetProperty("content").GetProperty("title").GetString());
        Assert.Equal("draft", summary.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ShouldHideDescriptionGivenAnotherExistingProgramRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var descriptionId = await fixture.SeedAsync();
        var otherProgram = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(otherProgram);

        // Act
        using var response = await fixture.GetAsync($"/{descriptionId}", otherProgram);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRequireReadPermissionGivenGetAndListRoutes()
    {
        // Arrange
        await using var fixture = new Fixture(allowed: false);
        var descriptionId = await fixture.SeedAsync();

        // Act
        using var get = await fixture.GetAsync($"/{descriptionId}");
        using var list = await fixture.GetAsync($"/{descriptionId}/versions");
        using var preview = await fixture.GetAsync($"/{descriptionId}/impact-preview");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
    }

    [Fact]
    public async Task ShouldDescribeScopedReadsAndIdentifiedWritesGivenDescriptionHttpContract()
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        using var document = JsonDocument.Parse(await fixture.OpenApiAsync());

        // Assert
        var paths = document.RootElement.GetProperty("paths");
        var route = paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}");
        var get = route.GetProperty("get");
        Assert.False(route.TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/create-system-description-draft").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{description_id}/review-system-description/{command_id}").TryGetProperty("put", out _));
        Assert.False(paths.TryGetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/system-descriptions/{system_description_id}", out _));
        var parameters = get.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Equal(["description_id", "program_id", "tenant_id"],
            parameters.Select(value => value.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.All(parameters, value =>
        {
            Assert.Equal("path", value.GetProperty("in").GetString());
            Assert.True(value.GetProperty("required").GetBoolean());
        });
    }

    [Fact]
    public async Task ShouldUseNativeUnit200AndCurrentAuthorizationGivenExactCreationAndRevisionRetries()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = await fixture.PrepareCreationAsync();

        // Act
        using var created = await fixture.PutAsync(request);
        using var replay = await fixture.PutAsync(request);
        var revision = new ReviseSystemDescriptionDraft(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, request.DescriptionId, 1,
            "Corrected HTTP title", request.Sections, "Correct narrative title");
        using var revised = await fixture.ReviseAsync(revision);
        fixture.Permissions.Allowed = false;
        using var denied = await fixture.ReviseAsync(revision);
        fixture.Permissions.Allowed = true;
        using var original = await fixture.PutAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("{}", await created.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal("{}", await original.Content.ReadAsStringAsync());
        Assert.Equal(2, await fixture.CountAsync(request.DescriptionId));
    }

    [Fact]
    public async Task ShouldRejectMalformedAndMissingRequiredLogicalIdentityGivenDescriptionRevisionRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.SeedAsync();

        // Act
        using var malformed = await fixture.InvalidRevisionAsync(id, "not-a-uuid", "{}");
        using var missingBody = await fixture.InvalidRevisionAsync(id, Uuid.CreateVersion4().ToString(), "{}");
        using var noId = await fixture.GetAsync("/not-a-uuid");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingBody.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noId.StatusCode);
        Assert.Equal(1, await fixture.CountAsync(id));
    }

    sealed class Fixture : IAsyncDisposable
    {
        static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        readonly WebApplicationFactory<global::Program> _factory;
        readonly HttpClient _client;
        readonly InMemoryEventStore _events = new();
        public MutablePermissionAuthorizer Permissions { get; } = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();

        public Fixture(bool allowed = true)
        {
            Permissions.Allowed = allowed;
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "system-description-read-http-tests");
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                                 descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                        services.Remove(descriptor);
                    services.RemoveAll<IEventStore>();
                    services.RemoveAll<IDomainEventReader>();
                    services.AddSingleton<IEventStore>(_events);
                    services.AddSingleton<IDomainEventReader>(_events);
                    services.RemoveAll<IKvClient>();
                    services.AddSingleton<IKvClient>(new InMemoryKvClient());
                    services.RemoveAll<ITenantMembershipDirectoryReader>();
                    services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
                    services.RemoveAll<ITenantActivity>();
                    services.AddSingleton<ITenantActivity, ActiveTenant>();
                    services.RemoveAll<IPermissionAuthorizer>();
                    services.AddSingleton<IPermissionAuthorizer>(Permissions);
                    services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                    services.AddSingleton<IAccessGrantPermissionAuthorizer>(provider =>
                        new PermissionBackedAccessGrantPermissionAuthorizer(provider.GetRequiredService<IPermissionAuthorizer>()));
                });
            });
            _client = _factory.CreateClient();
        }

        public async Task<Uuid> SeedAsync()
        {
            await LoginAndProgramAsync();
            await using var scope = _factory.Services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            var description = new SystemDescriptionRules(Tenant, Program, Uuid.CreateVersion4());
            Assert.Null(description.Create(Uuid.CreateVersion4(), new SystemDescriptionScope("type_i", Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, null),
                SystemDescriptionNativeFixture.Body("HTTP narrative"), Member, "Description author", Now));
            await DescriptionSeeds.WriteAsync(writer, content, description);
            return description.Id;
        }

        public async Task LoginAndProgramAsync()
        {
            using var loginBody = new StringContent("{\"email_address\":\"description-reader@example.com\"}",
                Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", loginBody);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await SeedProgramAsync(Program);
        }
        public async Task<CreateSystemDescriptionDraft> PrepareCreationAsync()
        {
            await LoginAndProgramAsync();
            await using var scope = _factory.Services.CreateAsyncScope();
            var examination = Uuid.CreateVersion4();
            var boundary = Uuid.CreateVersion4();
            var engagement = new Bdgrz.Compliance.Features.Examinations.ExaminationEngagement(Tenant, Program, examination);
            Assert.Null(engagement.Create(new Bdgrz.Compliance.Features.Examinations.ExaminationEngagementContent("type_i", "Audit LLP",
                new DateOnly(2026, 10, 18), null, ["security"], boundary, []), Member, "Examiner", Now));
            var preview = Bdgrz.Compliance.Tests.Features.Examinations.TypeIBaselinePreviewTestFactory.Create(Tenant, Program, examination,
                [new Bdgrz.Compliance.Features.Examinations.TypeIBaselineSourceReference("approved_boundary", boundary, Uuid.CreateVersion4(), 1, new string('a', 64))]);
            Assert.Null(engagement.FreezeBaseline(1, Uuid.CreateVersion4(), preview, "Frozen complete source scope", Member, "Examiner", Now));
            await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(engagement, new RequestDispatchContext(RequestActor.System));
            return new CreateSystemDescriptionDraft(Tenant, Program, Uuid.CreateVersion4(), examination, "HTTP created narrative", SystemDescriptionNativeFixture.Body().Sections);
        }
        public async Task<HttpResponseMessage> PutAsync(CreateSystemDescriptionDraft request)
        {
            using var body = new StringContent(JsonSerializer.Serialize(request, Bdgrz.Compliance.ComplianceCoreJsonContext.Default.CreateSystemDescriptionDraft), Encoding.UTF8, "application/json");
            return await _client.PutAsync($"/api/v1/tenants/{Tenant}/programs/{Program}/system-descriptions/{request.DescriptionId}/create-system-description-draft", body);
        }
        public async Task<HttpResponseMessage> ReviseAsync(ReviseSystemDescriptionDraft request)
        {
            using var body = new StringContent(JsonSerializer.Serialize(request, Bdgrz.Compliance.ComplianceCoreJsonContext.Default.ReviseSystemDescriptionDraft), Encoding.UTF8, "application/json");
            return await _client.PutAsync($"/api/v1/tenants/{Tenant}/programs/{Program}/system-descriptions/{request.DescriptionId}/revise-system-description-draft/{request.CommandId}", body);
        }
        public async Task<HttpResponseMessage> InvalidRevisionAsync(Uuid id, string suffix, string json)
        {
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PutAsync($"/api/v1/tenants/{Tenant}/programs/{Program}/system-descriptions/{id}/revise-system-description-draft/{suffix}", body);
        }
        public async Task<int> CountAsync(Uuid description)
        {
            var count = 0;
            await foreach (var record in _events.ReadAsync(EventStreamPattern.ForPattern(Tenant.ToString(), Bdgrz.Compliance.Features.SystemDescription.SystemDescription.Area,
                description.ToString()), EventCursor.Start, CancellationToken.None))
                if (!record.Event.Metadata.IsAudit)
                    count++;
            return count;
        }

        public async Task SeedProgramAsync(Uuid programId)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var program = new ComplianceProgram(Tenant, programId);
            Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
                Member, "Program lead", Now));
            await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(program,
                new RequestDispatchContext(RequestActor.System));
        }

        public Task<string> OpenApiAsync() => _client.GetStringAsync("/openapi/v1.json");

        public Task<HttpResponseMessage> GetAsync(string suffix, Uuid? program = null) =>
            _client.GetAsync($"/api/v1/tenants/{Tenant}/programs/{program ?? Program}/system-descriptions{suffix}");

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }
    }

    sealed class MutablePermissionAuthorizer : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
