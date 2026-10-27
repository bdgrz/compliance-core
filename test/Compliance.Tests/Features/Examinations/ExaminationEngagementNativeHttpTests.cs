using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
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

namespace Bdgrz.Compliance.Tests.Features.Examinations;

[Trait("Category", "WebIntegration")]
public sealed class ExaminationEngagementNativeHttpTests
{
    [Fact]
    public async Task ShouldReturnOriginalUnitAndNativeScopedTermsGivenAcceptedHttpMutations()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var create = fixture.Create();

        // Act
        using var first = await fixture.PutAsync(create);
        using var revised = await fixture.ReviseAsync(new ReviseExaminationEngagement(Uuid.CreateVersion4(), fixture.Tenant,
            fixture.Program, fixture.Examination, 1, "Updated audit firm", new DateOnly(2026, 9, 30), null, ["security"]));
        await fixture.SeedEntryAsync(ProgramReadiness.Defer);
        var before = await fixture.ReadAsync();
        using var replay = await fixture.PutAsync(create);
        using var read = await fixture.GetAsync($"?examination_engagement_id={Uuid.CreateVersion4()}&tenant_id={Uuid.CreateVersion4()}&program_id={Uuid.CreateVersion4()}");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("{}", (await first.Content.ReadAsStringAsync()).Trim());
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        Assert.Equal("{}", (await revised.Content.ReadAsStringAsync()).Trim());
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var body = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal("Updated audit firm", body.RootElement.GetProperty("audit_firm").GetString());
        Assert.Equal(fixture.Tenant.ToString(), body.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(fixture.Program.ToString(), body.RootElement.GetProperty("program_id").GetString());
        Assert.Equal(2, after.Owner.Revision);
        Assert.Equal(before.Owner.CommittedStreamPosition, after.Owner.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRequireCurrentProgramPermissionGivenAcceptedMutationRetry()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var request = fixture.Create();
        using var accepted = await fixture.PutAsync(request);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        fixture.Permissions.Allowed = false;

        // Act
        using var refused = await fixture.PutAsync(request);
        var owner = await fixture.ReadAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(1, owner.Owner.Revision);
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("firm_staff")]
    public async Task ShouldRejectManagementMutationGivenNonClientPersonalAffiliation(string affiliation)
    {
        // Arrange
        await using var fixture = new Fixture(affiliation);
        await fixture.SeedAsync();

        // Act
        using var refused = await fixture.PutAsync(fixture.Create());
        var owner = await fixture.ReadAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Null(owner.View);
        Assert.Equal(0, owner.Owner.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        readonly WebApplicationFactory<global::Program> _factory;
        readonly HttpClient _client;
        readonly InMemoryEventStore _events = new();
        readonly Uuid _member = Uuid.CreateVersion4();
        readonly Uuid _boundary = Uuid.CreateVersion4();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Examination { get; } = Uuid.CreateVersion4();
        public MutablePermissions Permissions { get; } = new();
        string Route => $"/api/v1/tenants/{Tenant}/programs/{Program}/examinations/{Examination}";

        public Fixture(string affiliation = "client_personnel")
        {
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "examination-native-http-tests");
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
                    services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true, affiliation));
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

        public async Task SeedAsync()
        {
            using var body = new StringContent("{\"email_address\":\"examination-lead@example.test\"}", Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", body);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await using var scope = _factory.Services.CreateAsyncScope();
            var program = new ProgramRules(Tenant, Program);
            Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), _member, "Program lead", Now));
            await ProgramSeeds.WriteAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
                scope.ServiceProvider.GetRequiredService<IAggregateWriter>(),
                scope.ServiceProvider.GetRequiredService<IDomainEventReader>(),
                scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), program);
            await SeedEntryAsync(ProgramReadiness.Approve);
        }
        public async Task SeedEntryAsync(string outcome)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            var assessment = Uuid.CreateVersion4();
            var approver = Uuid.CreateVersion4();
            await ReadinessSeed.AdoptAsync(reader, executor, content, Tenant, Program, assessment,
                new ReadinessEvaluation([], [], [], new string('a', 64)), Now, _member, Now);
            if (outcome != ProgramReadiness.Defer)
                await ReadinessSeed.DecideAsync(reader, executor, content, Tenant, Program, assessment,
                    ReadinessDecisionKind.Readiness, ProgramReadiness.Proceed, "Ready for examination", approver, Now);
            await ReadinessSeed.DecideAsync(reader, executor, content, Tenant, Program, assessment,
                ReadinessDecisionKind.TypeIEntry, outcome, "Type I entry", approver, Now);
        }
        public CreateExaminationEngagement Create() => new(Tenant, Program, Examination, "type_i", "Original audit firm",
            new DateOnly(2026, 9, 30), null, ["security"], _boundary);
        public Task<HttpResponseMessage> PutAsync(CreateExaminationEngagement request) => SendAsync(Route,
            JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.CreateExaminationEngagement));
        public Task<HttpResponseMessage> ReviseAsync(ReviseExaminationEngagement request) => SendAsync(Route + "/revisions",
            JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.ReviseExaminationEngagement));
        async Task<HttpResponseMessage> SendAsync(string route, string json)
        {
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PutAsync(route, body);
        }
        public Task<HttpResponseMessage> GetAsync(string query = "") => _client.GetAsync(Route + query);
        public async Task<ExaminationEngagementReadSnapshot> ReadAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ExaminationEngagementReader>().ReadAsync(Tenant, Program, Examination);
        }
        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }
    }
    public sealed class MutablePermissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenant, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}
