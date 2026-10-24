using Bdgrz.Compliance.Features.AuditPopulations;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Examinations;
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

namespace Bdgrz.Compliance.Tests.Features.Operations;

[Trait("Category", "WebIntegration")]
public sealed class ObservationPeriodReadHttpTests
{
    [Fact]
    public async Task ShouldUseCanonicalPeriodRouteGivenMisleadingQueryIdentities()
    {
        // Arrange
        await using var fixture = new Fixture();
        var periodId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync($"/{periodId}?observation_period_id={Uuid.CreateVersion4()}&" +
            $"tenant_id={Uuid.CreateVersion4()}&program_id={Uuid.CreateVersion4()}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(periodId.ToString(), body.RootElement.GetProperty("observation_period_id").GetString());
        Assert.Equal(fixture.Tenant.ToString(), body.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(fixture.Program.ToString(), body.RootElement.GetProperty("program_id").GetString());
    }

    [Fact]
    public async Task ShouldReturnActualNativeSummaryGivenProductionScopedListRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var periodId = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync(string.Empty);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(periodId.ToString(), summary.GetProperty("observation_period_id").GetString());
        Assert.Equal("active", summary.GetProperty("status").GetString());
        Assert.Equal(1, summary.GetProperty("revision").GetInt64());
        Assert.Equal("2026-10-01", summary.GetProperty("period_start").GetString());
        Assert.Equal("2027-09-30", summary.GetProperty("period_end").GetString());
    }

    [Fact]
    public async Task ShouldHidePeriodGivenAnotherExistingProgramRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var periodId = await fixture.SeedAsync();
        var otherProgram = Uuid.CreateVersion4();
        await fixture.SeedProgramAsync(otherProgram);

        // Act
        using var response = await fixture.GetAsync($"/{periodId}", otherProgram);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRequireReadPermissionGivenGetAndListRoutes()
    {
        // Arrange
        await using var fixture = new Fixture(allowed: false);
        var periodId = await fixture.SeedAsync();

        // Act
        using var get = await fixture.GetAsync($"/{periodId}");
        using var list = await fixture.GetAsync(string.Empty);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task ShouldDescribeOnlyCanonicalRouteIdentityGivenCustomPeriodReadBinder()
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        using var document = JsonDocument.Parse(await fixture.OpenApiAsync());

        // Assert
        var paths = document.RootElement.GetProperty("paths");
        var route = paths.GetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{period_id}");
        var get = route.GetProperty("get");
        Assert.False(paths.TryGetProperty("/api/v1/tenants/{tenant_id}/programs/{program_id}/observation-periods/{observation_period_id}", out _));
        var parameters = get.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Equal(["period_id", "program_id", "tenant_id"],
            parameters.Select(value => value.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.All(parameters, value =>
        {
            Assert.Equal("path", value.GetProperty("in").GetString());
            Assert.True(value.GetProperty("required").GetBoolean());
        });
    }

    [Fact]
    public async Task ShouldReadExactHistoricalSnapshotGivenScopedCloseRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        var periodId = await fixture.SeedAsync(closePeriod: true);

        // Act
        using var response = await fixture.GetCloseAsync(fixture.CloseId!.Value);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(periodId.ToString(), body.RootElement.GetProperty("period_id").GetString());
        Assert.Equal(fixture.CloseId.Value.ToString(), body.RootElement.GetProperty("observation_period_close_id").GetString());
        Assert.Equal(fixture.CloseId.Value.ToString(), body.RootElement.GetProperty("snapshot").GetProperty("close_snapshot_id").GetString());
        Assert.Single(body.RootElement.GetProperty("close_snapshots").EnumerateArray());
    }

    [Fact]
    public async Task ShouldRequireCurrentProgramReadPermissionGivenHistoricalCloseRoute()
    {
        // Arrange
        await using var fixture = new Fixture(allowed: false);
        await fixture.SeedAsync(closePeriod: true);

        // Act
        using var response = await fixture.GetCloseAsync(fixture.CloseId!.Value);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ShouldReplayOriginalUnitBodyGivenScopedPersonalGenerationRouteAndExactRetry()
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedAsync();

        // Act
        using var first = await fixture.GenerateAsync(period, "initial-empty-selection");
        using var retry = await fixture.GenerateAsync(period, "initial-empty-selection");
        using var current = await fixture.GetAsync($"/{period}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("{}", await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("{}", await retry.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task ShouldRequireCurrentProgramReadPermissionGivenScopedClosePreview()
    {
        // Arrange
        await using var fixture = new Fixture(allowed: false);
        var period = await fixture.SeedAsync();

        // Act
        using var response = await fixture.GetAsync($"/{period}/close-preview");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        public Uuid? CloseId { get; private set; }

        public Fixture(bool allowed = true)
        {
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "observation-period-read-http-tests");
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
                    services.AddSingleton<TimeProvider>(new FixedClock(Now.AddHours(1)));
                    services.RemoveAll<IPermissionAuthorizer>();
                    services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(allowed));
                    services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                    services.AddSingleton<IAccessGrantPermissionAuthorizer>(provider =>
                        new PermissionBackedAccessGrantPermissionAuthorizer(provider.GetRequiredService<IPermissionAuthorizer>()));
                });
            });
            _client = _factory.CreateClient();
        }

        public async Task<Uuid> SeedAsync(bool closePeriod = false)
        {
            using var loginBody = new StringContent("{\"email_address\":\"period-reader@example.com\"}",
                Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", loginBody);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await SeedProgramAsync(Program);
            await using var scope = _factory.Services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            var plan = ApprovedPlan();
            var period = new TypeIIPeriodRules(Tenant, Program, Uuid.CreateVersion4());
            var source = new ExaminationContentReference("control", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('a', 64));
            Assert.Null(period.Start(plan.Current!, new TypeIIPeriodOpeningContent(plan.Current!.Current!.Content.ExaminationId,
                Uuid.CreateVersion4(), new string('b', 64), [source], []), Member, "Period manager", Now));
            await TypeIIPeriodSeeds.WriteAsync(writer, content, period);
            if (closePeriod)
            {
                var expected = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
                var at = Now.AddYears(1);
                Assert.Null(period.RecordCloseValidation(period.Revision, new TypeIIPeriodCloseValidationContent([source], expected,
                    expected, [], [], []), Uuid.CreateVersion4(), "Close validator", at));
                Assert.Null(period.ApproveClose(period.Revision, [], "Reviewed exact close.", Member, "CEO", "Executive approver", at.AddMinutes(1)));
                CloseId = Uuid.CreateVersion4();
                Assert.Null(period.FreezeClose(period.Revision, new TypeIIPeriodCloseSnapshotContent(CloseId.Value, [source], expected, []),
                    Member, "CEO", at.AddMinutes(2)));
                await TypeIIPeriodSeeds.AppendAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), writer, content, period);
            }
            var projection = scope.ServiceProvider.GetRequiredService<IObservationPeriodDirectoryProjection>();
            var identity = new CheckpointIdentity(FitzObservationPeriodDirectory.ProjectorName,
                EventStreamPattern.ForPattern(Tenant.ToString(), TypeIIPeriod.Area));
            await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity,
                ProjectionCheckpoint.Start));
            var cursor = EventCursor.Start;
            await foreach (var record in _events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
            {
                if (record.Event is ObservationPeriodStarted created)
                    await projection.ApplyAsync(created);
                else if (record.Event is ObservationPeriodCloseFrozen frozen)
                    await projection.ApplyAsync(frozen);
                else if (record.Event is TypeIIPeriodCloseAmended amended)
                    await projection.ApplyAsync(amended);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
            return period.Id;
        }

        TypeIIPlan ApprovedPlan()
        {
            var plan = new TypeIIPlan(Tenant, Program, Uuid.CreateVersion4());
            var start = new DateOnly(2026, 10, 1);
            var end = new DateOnly(2027, 9, 30);
            var source = new ExaminationContentReference("control", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('a', 64));
            var terms = new TypeIIPlanContent(Uuid.CreateVersion4(), Uuid.CreateVersion4(), new string('a', 64), Uuid.CreateVersion4(), start, end,
                "Audit Firm", ["security"], Uuid.CreateVersion4(), Uuid.CreateVersion4(), [source],
                [new TypeIIControlPlanEntry(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Quarterly control", Member,
                    "quarterly", 4, start, null, ExpectedEvidenceDescriptions: ["Dated review"])], [], [], []);
            Assert.Null(plan.Propose(terms, Member, "Plan author", Now.AddHours(-3)));
            Assert.Null(plan.Review(plan.Revision, 1, "approve", "Reviewed exact plan.", Uuid.CreateVersion4(), "Independent reviewer", Now.AddHours(-2)));
            Assert.Null(plan.Approve(plan.Revision, 1, "Approved exact plan.", Uuid.CreateVersion4(), "CISO", "Executive approver", Now.AddHours(-1)));
            return plan;
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
            _client.GetAsync($"/api/v1/tenants/{Tenant}/programs/{program ?? Program}/observation-periods{suffix}");

        public async Task<HttpResponseMessage> GenerateAsync(Uuid period, string key)
        {
            using var body = new StringContent("{\"through_date\":\"2026-10-01\"}", Encoding.UTF8, "application/json");
            return await _client.PutAsync($"/api/v1/tenants/{Tenant}/programs/{Program}/observation-periods/{period}/occurrence-generations/{key}", body);
        }

        public Task<HttpResponseMessage> GetCloseAsync(Uuid closeId) => _client.GetAsync(
            $"/api/v1/tenants/{Tenant}/programs/{Program}/observation-period-closes/{closeId}");

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
    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
