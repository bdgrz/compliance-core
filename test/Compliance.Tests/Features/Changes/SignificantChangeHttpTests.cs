using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Changes;
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

namespace Bdgrz.Compliance.Tests.Features.Changes;

[Trait("Category", "WebIntegration")]
public sealed class SignificantChangeHttpTests
{
    [Fact]
    public async Task ShouldUseCanonicalChangeRouteGivenMisleadingQueryIdentities()
    {
        // Arrange
        await using var fixture = new Fixture();
        var changeId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync($"/{changeId}?change_id={Uuid.CreateVersion4()}&" +
            $"tenant_id={Uuid.CreateVersion4()}&program_id={Uuid.CreateVersion4()}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(changeId.ToString(), body.RootElement.GetProperty("significant_change_id").GetString());
        Assert.Equal(fixture.Tenant.ToString(), body.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(fixture.Program.ToString(), body.RootElement.GetProperty("program_id").GetString());
    }

    [Fact]
    public async Task ShouldReturnActualNativeSummaryGivenProductionScopedListRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var changeId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync(string.Empty);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(changeId.ToString(), summary.GetProperty("significant_change_id").GetString());
        Assert.Equal("Governed production change", summary.GetProperty("name").GetString());
        Assert.Equal("recorded", summary.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ShouldHideChangeGivenAnotherExistingProgramRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var changeId = await fixture.SeedAsync();
        var otherProgram = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(otherProgram);

        // Act
        using var response = await fixture.GetAsync($"/{changeId}", otherProgram);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRequireReadPermissionGivenGetAndListRoutes()
    {
        // Arrange
        await using var fixture = new Fixture(allowed: false);
        var changeId = await fixture.SeedAsync();

        // Act
        using var get = await fixture.GetAsync($"/{changeId}");
        using var list = await fixture.GetAsync(string.Empty);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task ShouldDescribeCanonicalRouteAndIdentifiedMutationsGivenChangeHttpContract()
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        using var document = JsonDocument.Parse(await fixture.OpenApiAsync());

        // Assert
        var paths = document.RootElement.GetProperty("paths");
        var route = paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}");
        var get = route.GetProperty("get");
        Assert.True(route.TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/revisions/{command_id}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{change_id}/assessments/{command_id}").TryGetProperty("put", out _));
        Assert.False(paths.TryGetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/significant-changes/{significant_change_id}", out _));
        var parameters = get.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Equal(["change_id", "program_id", "tenant_id"],
            parameters.Select(value => value.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.All(parameters, value =>
        {
            Assert.Equal("path", value.GetProperty("in").GetString());
            Assert.True(value.GetProperty("required").GetBoolean());
        });
    }

    [Fact]
    public async Task ShouldReturnTypedUnitAndReplayGivenIdentifiedProductionCreationRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = new RecordSignificantChange(fixture.Tenant, fixture.Program, Uuid.CreateVersion4(),
            new SignificantChangeContent(null, null, null, "HTTP native change", "Complete governed content.", new DateOnly(2026, 10, 18),
                null, fixture.Member, [Uuid.CreateVersion4()], "high", "confidential", []));

        // Act
        using var first = await fixture.PutAsync(request);
        using var replay = await fixture.PutAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("{}", await first.Content.ReadAsStringAsync());
        Assert.Equal("{}", await replay.Content.ReadAsStringAsync());
        Assert.Equal(1, await fixture.CountAsync(request.ChangeId));
    }

    sealed class Fixture : IAsyncDisposable
    {
        static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        readonly WebApplicationFactory<global::Program> _factory;
        readonly HttpClient _client;
        readonly InMemoryEventStore _events = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();

        public Fixture(bool allowed = true)
        {
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "significant-change-read-http-tests");
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
                    services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(allowed));
                    services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                    services.AddSingleton<IAccessGrantPermissionAuthorizer>(provider =>
                        new PermissionBackedAccessGrantPermissionAuthorizer(provider.GetRequiredService<IPermissionAuthorizer>()));
                });
            });
            _client = _factory.CreateClient();
        }

        public async Task<Uuid> SeedAsync()
        {
            using var loginBody = new StringContent("{\"email_address\":\"change-reader@example.com\"}",
                Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", loginBody);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await SeedProgramAsync(Program);
            await using var scope = _factory.Services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            var package = new SignificantChangeRules(Tenant, Program, Uuid.CreateVersion4());
            Assert.Null(package.Record(new SignificantChangeContent(null, null, null, "Governed production change", "A complete change body.",
                new DateOnly(2026, 10, 18), null, Member, [Uuid.CreateVersion4()], "high", "confidential", []), Member, "Change recorder", Now));
            await ChangeSeeds.WriteAsync(writer, content, package);
            var projection = scope.ServiceProvider.GetRequiredService<ISignificantChangeDirectoryProjection>();
            var identity = new CheckpointIdentity(SignificantChangeDirectoryProjector.ProjectorName,
                EventStreamPattern.ForPattern(Tenant.ToString(), SignificantChange.Area));
            await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity,
                ProjectionCheckpoint.Start));
            var cursor = EventCursor.Start;
            await foreach (var record in _events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
            {
                if (record.Event is ISignificantChangeFact)
                    await projection.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
            return package.Id;
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

        public async Task<HttpResponseMessage> PutAsync(RecordSignificantChange request)
        {
            using var body = new StringContent(JsonSerializer.Serialize(request, Bdgrz.Compliance.ComplianceCoreJsonContext.Default.RecordSignificantChange), Encoding.UTF8, "application/json");
            return await _client.PutAsync($"/api/v1/tenants/{Tenant}/programs/{Program}/significant-changes/{request.ChangeId}", body);
        }
        public async Task<int> CountAsync(Uuid changeId)
        {
            var count = 0;
            await foreach (var record in _events.ReadAsync(EventStreamPattern.ForPattern(Tenant.ToString(), SignificantChange.Area, changeId.ToString()), EventCursor.Start, CancellationToken.None))
                if (!record.Event.Metadata.IsAudit)
                    count++;
            return count;
        }

        public Task<string> OpenApiAsync() => _client.GetStringAsync("/openapi/v1.json");

        public Task<HttpResponseMessage> GetAsync(string suffix, Uuid? program = null) =>
            _client.GetAsync($"/api/v1/tenants/{Tenant}/programs/{program ?? Program}/significant-changes{suffix}");

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
