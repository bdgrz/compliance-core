using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Tests.E2E;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

[Trait("Category", "WebIntegration")]
public sealed class WorkDigestAuthorizationHttpTests
{
    [Fact]
    public async Task ShouldReturnFrozenNativeBytesAndCurrentRevisionGivenIdentifiedOperatorRetryAfterProgress()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAndSeedAsync();
        var command = Uuid.CreateVersion4();
        var route = fixture.Route(command);
        var body = Body(3);
        using var first = await fixture.Client.PutAsJsonAsync(route, body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var bytes = await first.Content.ReadAsByteArrayAsync();
        await fixture.MutateAsync(owner => owner.StartAttempt(fixture.Week, fixture.At.AddMinutes(4)));
        await fixture.MutateAsync(owner => owner.RecordUnknown(fixture.Week, fixture.At.AddMinutes(5), "later_ambiguous"));

        // Act
        using var replay = await fixture.Client.PutAsJsonAsync(route, body);
        using var current = await fixture.Client.GetAsync(fixture.StatusRoute);
        using var conflict = await fixture.Client.PutAsJsonAsync(route, Body(3, "Another rationale"));
        var fact = Assert.Single((await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>());
        var descriptor = await FirstResponses.ReplayAsync(fixture.Content, fixture.Tenant.ToString(), fact.FirstResponse!);
        var frozen = await fixture.Content.ReadVerifiedAsync(fixture.Tenant.ToString(), Assert.IsType<StandardResponseBody>(descriptor.Body).Content);
        var envelope = await fixture.Content.ReadJsonAsync<MutationResultContent>(fixture.Tenant.ToString(), fact.OperationResult!);

        // Assert
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(bytes, await replay.Content.ReadAsByteArrayAsync());
        Assert.Equal(frozen.ToArray(), bytes);
        Assert.Equal("application/json", replay.Content.Headers.ContentType?.ToString());
        Assert.Equal(WorkDigestRetryContent.ResultSchema, envelope.Schema);
        Assert.Equal(descriptor.Body, envelope.Value);
        Assert.Equal(command, fact.Operation.LogicalId);
        using var originalBody = JsonDocument.Parse(bytes);
        Assert.Equal(1, originalBody.RootElement.GetProperty("attempts").GetInt32());
        Assert.Equal(4, originalBody.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        using var currentBody = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        Assert.Equal(2, currentBody.RootElement.GetProperty("attempts").GetInt32());
        Assert.Equal("unknown", currentBody.RootElement.GetProperty("status").GetString());
        Assert.Equal(6, currentBody.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task ShouldUseCanonicalRouteScopeGivenConflictingBodyAndQueryIdentities()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAndSeedAsync();
        var command = Uuid.CreateVersion4();
        var body = Body(3);
        body["command_id"] = Uuid.CreateVersion4().ToString();
        body["tenant_id"] = Uuid.CreateVersion4().ToString();
        body["member_id"] = Uuid.CreateVersion4().ToString();
        body["week_of"] = "2026-10-19";

        // Act
        using var response = await fixture.Client.PutAsJsonAsync(fixture.Route(command) + $"?command_id={Uuid.CreateVersion4()}&tenant_id={Uuid.CreateVersion4()}", body);
        using var read = await fixture.Client.GetAsync(fixture.StatusRoute + $"?tenant_id={Uuid.CreateVersion4()}&member_id={Uuid.CreateVersion4()}&week_of=2026-10-19");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var value = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(fixture.Tenant.ToString(), value.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(fixture.Member.ToString(), value.RootElement.GetProperty("member_id").GetString());
        Assert.Equal("2026-10-12", value.RootElement.GetProperty("week_of").GetString());
        Assert.Equal(command, Assert.Single((await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>()).Operation.LogicalId);
    }

    [Fact]
    public async Task ShouldAuthorizeCurrentOperatorBeforeOriginalReadsGivenIdentifiedHttpReplay()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAndSeedAsync();
        var route = fixture.Route(Uuid.CreateVersion4());
        using var first = await fixture.Client.PutAsJsonAsync(route, Body(3));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var reads = fixture.Content.Reads;
        fixture.Operators.Allowed = false;

        // Act
        using var denied = await fixture.Client.PutAsJsonAsync(route, Body(3));
        using var anonymous = fixture.Factory.CreateClient();
        using var unauthorized = await anonymous.PutAsJsonAsync(route, Body(3));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(reads, fixture.Content.Reads);
        Assert.Single((await fixture.FactsAsync()).OfType<WorkDigestUnknownRetryAuthorized>());
    }

    [Fact]
    public async Task ShouldExposeOnlySelectedIdentifiedPutGivenDigestAuthorizationHttpContract()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAndSeedAsync();

        // Act
        using var old = await fixture.Client.PostAsJsonAsync(fixture.StatusRoute + "/retry-authorizations", Body(3));
        using var document = JsonDocument.Parse(await fixture.Client.GetStringAsync("/openapi/v1.json"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
        var paths = document.RootElement.GetProperty("paths");
        Assert.False(paths.TryGetProperty("/api/v1/platform/tenants/{tenant_id}/members/{member_id}/work-digest-dispatches/{week_of}/retry-authorizations", out _));
        var selected = paths.GetProperty("/api/v1/platform/tenants/{tenant_id}/members/{member_id}/work-digest-dispatches/{week_of}/retry-authorizations/{command_id}");
        Assert.True(selected.TryGetProperty("put", out var put));
        Assert.False(selected.TryGetProperty("post", out _));
        Assert.Equal(["command_id", "member_id", "tenant_id", "week_of"], put.GetProperty("parameters").EnumerateArray()
            .Where(parameter => parameter.GetProperty("in").GetString() == "path")
            .Select(parameter => parameter.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
    }

    static Dictionary<string, object> Body(long revision, string rationale = "Verified relay receipt") => new()
    {
        ["expected_revision"] = revision,
        ["evidence_reference"] = "receipt:http",
        ["rationale"] = rationale
    };

    sealed class Fixture : IAsyncDisposable
    {
        readonly InMemoryEventStore _events = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();
        public DateOnly Week { get; } = new(2026, 10, 12);
        public DateTimeOffset At { get; } = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        public OperatorAccess Operators { get; } = new();
        public ReadCountingContent Content { get; } = new(new FitzKvImmutableContentStore(new InMemoryKvClient()));
        public WebApplicationFactory<global::Program> Factory { get; }
        public HttpClient Client { get; }
        public string StatusRoute => $"/api/v1/platform/tenants/{Tenant}/members/{Member}/work-digest-dispatches/2026-10-12";
        public string Route(Uuid command) => StatusRoute + $"/retry-authorizations/{command}";
        public Fixture()
        {
            Factory = ProviderHttpMcpTests.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.AddSingleton<IEventStore>(_events);
                services.AddSingleton<IDomainEventReader>(_events);
                services.RemoveAll<IImmutableContentStore>();
                services.AddSingleton<IImmutableContentStore>(Content);
                services.RemoveAll<IPlatformOperatorAccess>();
                services.AddSingleton<IPlatformOperatorAccess>(Operators);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new Clock(At.AddMinutes(3)));
            }));
            Client = Factory.CreateClient();
        }
        public async Task LoginAndSeedAsync()
        {
            _ = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(Client, "digest-operator@example.test"), CultureInfo.InvariantCulture);
            await MutateAsync(owner => owner.Schedule(new WorkDigestScheduleWindow(Week, "UTC", At), Uuid.CreateVersion4(), TimeSpan.FromDays(1), At));
            await MutateAsync(owner => owner.StartAttempt(Week, At.AddMinutes(1)));
            await MutateAsync(owner => owner.RecordUnknown(Week, At.AddMinutes(2), "ambiguous"));
        }
        public async Task MutateAsync(Func<WorkDigestDispatch, bool> mutation)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(new WorkDigestDispatch(Tenant, Member), owner =>
            {
                Assert.True(mutation(owner));
                return AggregateOutcome.Commit(Result.Success);
            }, new Cntryl.Portia.RequestContext<Mutation>(new Mutation(), RequestActor.System));
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
        public async Task<IReadOnlyList<DomainEvent>> FactsAsync()
        {
            var facts = new List<DomainEvent>();
            await foreach (var record in _events.ReadAsync(new WorkDigestDispatch(Tenant, Member).Stream, 0, CancellationToken.None))
                if (!record.Event.Metadata.IsAudit)
                    facts.Add(record.Event);
            return facts;
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }
    sealed record Mutation : IRequest;
    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    sealed class OperatorAccess : IPlatformOperatorAccess
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
    sealed class ReadCountingContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            Reads++;
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
