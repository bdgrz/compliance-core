using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.E2E;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

[Trait("Category", "WebIntegration")]
public sealed class PersonNativeHttpTests
{
    [Fact]
    public async Task ShouldReturnOriginalPerson200GivenCanonicalCreationRouteAndDifferentQueryIdentities()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var person = Uuid.CreateVersion4();
        var route = fixture.Route(person);
        const string json = "{\"display_name\":\"Grace\",\"work_email\":\"grace@example.com\"}";

        // Act
        using var first = await fixture.PutAsync(route, json);
        using var retry = await fixture.PutAsync(route + $"?person_id={Uuid.CreateVersion4()}&tenant_id={Uuid.CreateVersion4()}", json);
        var original = await first.Content.ReadAsStringAsync();
        var replay = await retry.Content.ReadAsStringAsync();

        // Assert
        Assert.True(first.StatusCode == HttpStatusCode.OK, original);
        Assert.True(retry.StatusCode == HttpStatusCode.OK, replay);
        Assert.Equal(original, replay);
        using var body = JsonDocument.Parse(original);
        Assert.Equal(person.ToString(), body.RootElement.GetProperty("person_id").GetString());
        Assert.Equal(1UL, (await fixture.OwnerAsync(person)).Version);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("correlation")]
    public async Task ShouldReturnOriginalTypedUnit200GivenConsumedRevisionAndLaterPersonChange(string kind)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var person = Uuid.CreateVersion4();
        using var created = await fixture.PutAsync(fixture.Route(person), "{\"display_name\":\"Grace\"}");
        Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var route = kind == "revision" ? fixture.Route(person) + $"/revisions/{Uuid.CreateVersion4()}" :
            fixture.Route(person) + $"/membership-correlations/{Uuid.CreateVersion4()}";
        var json = kind == "revision" ? "{\"expected_revision\":1,\"display_name\":\"Grace Hopper\"}" :
            $$"""{"expected_revision":1,"user_id":"{{fixture.UserId}}"}""";
        using var first = await fixture.PutAsync(route, json);
        var original = await first.Content.ReadAsStringAsync();
        Assert.True(first.StatusCode == HttpStatusCode.OK, original);
        using var later = await fixture.PutAsync(fixture.Route(person) + $"/revisions/{Uuid.CreateVersion4()}",
            "{\"expected_revision\":2,\"display_name\":\"Rear Admiral Grace Hopper\"}");
        Assert.True(later.StatusCode == HttpStatusCode.OK, await later.Content.ReadAsStringAsync());

        // Act
        using var retry = await fixture.PutAsync(route, json);
        var replay = await retry.Content.ReadAsStringAsync();

        // Assert
        Assert.True(retry.StatusCode == HttpStatusCode.OK, replay);
        Assert.Equal(original, replay);
        using var body = JsonDocument.Parse(original);
        Assert.Equal(JsonValueKind.Object, body.RootElement.ValueKind);
        Assert.Empty(body.RootElement.EnumerateObject());
        Assert.Equal(3, (await fixture.OwnerAsync(person)).Revision);
        Assert.Equal(3UL, (await fixture.OwnerAsync(person)).Version);
    }

    [Fact]
    public async Task ShouldRejectConflictingIdentityAndReauthorizeGivenAcceptedPersonHttpCreation()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var person = Uuid.CreateVersion4();
        var route = fixture.Route(person);
        const string json = "{\"display_name\":\"Grace\"}";
        using var accepted = await fixture.PutAsync(route, json);
        Assert.True(accepted.StatusCode == HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());

        // Act
        using var conflict = await fixture.PutAsync(route, $$"""{"person_id":"{{Uuid.CreateVersion4()}}","display_name":"Grace"}""");
        using var missing = await fixture.PutAsync($"/api/v1/tenants/{fixture.TenantId}/people", json);
        fixture.Permissions.Allowed = false;
        using var denied = await fixture.PutAsync(route, json);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        Assert.True(missing.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(1UL, (await fixture.OwnerAsync(person)).Version);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; private set; }
        public AttestWorkforceManagementWallTests.Permissions Permissions { get; } = new();
        public WebApplicationFactory<global::Program> Factory { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var events = new InMemoryEventStore();
            fixture.Factory = ProviderHttpMcpTests.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.AddSingleton<IEventStore>(events);
                services.AddSingleton<IDomainEventReader>(events);
                services.RemoveAll<IPermissionAuthorizer>();
                services.AddSingleton<IPermissionAuthorizer>(fixture.Permissions);
                services.RemoveAll<ITenantActivity>();
                services.AddSingleton<ITenantActivity, ActiveTenant>();
                services.RemoveAll<ITenantMembershipDirectoryReader>();
                services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(fixture));
            }));
            fixture.Client = fixture.Factory.CreateClient();
            fixture.UserId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(fixture.Client, "person-native-client@example.test"), CultureInfo.InvariantCulture);
            await ProgramManagementServices.SeedAsync(fixture.Factory.Services, new Member(fixture.TenantId, fixture.UserId), member => member.Register());
            return fixture;
        }
        public string Route(Uuid person) => $"/api/v1/tenants/{TenantId}/people/{person}";
        public Task<HttpResponseMessage> PutAsync(string route, string json) => Client.PutAsync(route, new StringContent(json, Encoding.UTF8, "application/json"));
        public async Task<Person> OwnerAsync(Uuid id)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new Person(TenantId, id));
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }
    sealed class Memberships(Fixture fixture) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == fixture.TenantId.ToString() && userId == fixture.UserId
                ? new(userId, fixture.TenantId, "client_personnel", false) : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == fixture.TenantId.ToString() && userId == fixture.UserId);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
