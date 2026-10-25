using System.Net;
using System.Text;
using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

[Trait("Category", "WebIntegration")]
public sealed class IndependenceRuleCatalogHttpTests
{
    [Fact]
    public async Task ShouldReplayOriginal200BytesAndRequireCurrentOperatorGivenIdentifiedRuleRevisionAfterLaterState()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAsync();
        var original = new ReviseIndependenceRules(Uuid.CreateVersion4(), 0, Rules("Original source"));
        using var first = await fixture.ReviseAsync(original);
        var body = await first.Content.ReadAsStringAsync();
        using var later = await fixture.ReviseAsync(new(Uuid.CreateVersion4(), 1, Rules("Later source")));

        // Act
        using var replay = await fixture.ReviseAsync(original);
        using var changed = await fixture.ReviseAsync(original with { Content = Rules("Changed original intent") });
        fixture.Operators.Allowed = false;
        using var denied = await fixture.ReviseAsync(original);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(body, await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var history = await fixture.HistoryAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(1, history[0].Version);
        Assert.Equal("Original source", history[0].Content.SourceReference);
        Assert.Equal("Later source", history[1].Content.SourceReference);
        Assert.Equal(2, await fixture.CountAsync());
    }

    [Fact]
    public async Task ShouldRequireAuthoredUuidAndValidBodyGivenProductionRuleRevisionRoute()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.LoginAsync();
        var request = new ReviseIndependenceRules(Uuid.CreateVersion4(), 0, Rules("Valid source"));

        // Act
        using var missing = await fixture.PutAsync("/api/v1/platform/independence/rules", JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.ReviseIndependenceRules));
        using var malformedId = await fixture.PutAsync("/api/v1/platform/independence/rules/not-a-uuid", JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.ReviseIndependenceRules));
        using var malformedBody = await fixture.PutAsync($"/api/v1/platform/independence/rules/{request.CommandId}", "{\"expected_sequence\":0,\"content\":");
        using var created = await fixture.ReviseAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedId.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedBody.StatusCode);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(1, await fixture.CountAsync());
    }

    static IndependenceRuleContent Rules(string source) => new(12,
        [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")], source);

    sealed class Fixture : IAsyncDisposable
    {
        readonly InMemoryEventStore _events = new();
        readonly WebApplicationFactory<global::Program> _factory;
        readonly HttpClient _client;
        public MutableOperators Operators { get; } = new();
        public Fixture()
        {
            _factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "native-rule-catalog-http-tests");
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
                    services.RemoveAll<IPlatformOperatorAccess>();
                    services.AddSingleton<IPlatformOperatorAccess>(Operators);
                });
            });
            _client = _factory.CreateClient();
        }
        public async Task LoginAsync()
        {
            using var body = new StringContent("{\"email_address\":\"rule-catalog-native@example.com\"}", Encoding.UTF8, "application/json");
            using var result = await _client.PostAsync("/api/v1/developer-user-sessions", body);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }
        public Task<HttpResponseMessage> ReviseAsync(ReviseIndependenceRules request) => PutAsync(
            $"/api/v1/platform/independence/rules/{request.CommandId}", JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.ReviseIndependenceRules));
        public async Task<HttpResponseMessage> PutAsync(string path, string json)
        {
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            return await _client.PutAsync(path, body);
        }
        public async Task<IReadOnlyList<IndependenceRuleVersionView>> HistoryAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IndependenceRuleCatalogReader>().ReadVersionsAsync();
        }
        public async Task<int> CountAsync()
        {
            var stream = new IndependenceRuleCatalog().Stream;
            var count = 0;
            await foreach (var record in _events.ReadAsync(EventStreamPattern.ForPattern(stream.Realm, stream.Area, stream.Resource), EventCursor.Start, CancellationToken.None))
                if (record.Event is IndependenceRulesRevised)
                    count++;
            return count;
        }
        public async ValueTask DisposeAsync() { _client.Dispose(); await _factory.DisposeAsync(); }
    }
    sealed class MutableOperators : IPlatformOperatorAccess
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
}
