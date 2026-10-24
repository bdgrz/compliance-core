using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bdgrz.Compliance.Tests.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

[Trait("Category", "WebIntegration")]
public sealed class TenantInvitationTransportTests
{
    const string InvitationId = "b4eb443f-94d4-4ca6-aec8-06d7946ff1da";
    const string AnotherId = "dc97120d-631c-4aa7-ae05-ecf9e5d18791";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayInvitationAndAppendOnceGivenHttpOrMcpRetries(bool mcp)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = mcp ? await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp")) : null;
        var tenant = Uuid.CreateVersion4();
        var invitation = Uuid.CreateVersion4();
        await SendAsync();

        // Act
        await SendAsync();
        var facts = new List<DomainEvent>();
        await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                           .ReadAsync(new TenantInvitation(tenant, invitation).Stream, 0))
            facts.Add(record.Event);

        // Assert
        var invited = Assert.IsType<TenantMemberInvited>(Assert.Single(facts));
        Assert.Equal(invitation, invited.InvitationId);
        Assert.Equal("transport@example.com", invited.EmailAddress);

        async Task SendAsync()
        {
            if (scenario is not null)
            {
                await scenario.When("bdgrz.organization-member.invite", new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.ToString(),
                    ["invitation_id"] = invitation.ToString(),
                    ["email_address"] = "transport@example.com",
                    ["built_in_role"] = BuiltInRbac.ComplianceManagementRole,
                }).ExpectSuccess();
                return;
            }

            using var response = await client.PutAsJsonAsync($"/api/v1/tenants/{tenant}/member-invitations/{invitation}",
                new { email_address = "transport@example.com", built_in_role = BuiltInRbac.ComplianceManagementRole });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("malformed", "", HttpStatusCode.BadRequest)]
    [InlineData("00000000-0000-0000-0000-000000000000", "", HttpStatusCode.BadRequest)]
    [InlineData(InvitationId, "", HttpStatusCode.NoContent)]
    [InlineData(InvitationId, ",\"invitation_id\":\"" + InvitationId + "\"", HttpStatusCode.NoContent)]
    [InlineData(InvitationId, ",\"invitation_id\":\"" + AnotherId + "\"", HttpStatusCode.BadRequest)]
    [InlineData(InvitationId, ",\"invitation_id\":null", HttpStatusCode.BadRequest)]
    [InlineData(InvitationId, ",\"invitation_id\":\"malformed\"", HttpStatusCode.BadRequest)]
    public async Task ShouldValidateRequiredRouteIdentityGivenAnAuthorizedHttpInvitation(
        string invitationId, string duplicate, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        using var body = new StringContent("{\"email_address\":\"route@example.com\",\"built_in_role\":\"" +
            BuiltInRbac.ComplianceManagementRole + "\"" + duplicate + "}", Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PutAsync($"/api/v1/tenants/{tenant}/member-invitations/{invitationId}", body);
        var facts = new List<DomainEvent>();
        if (Uuid.TryParse(invitationId, System.Globalization.CultureInfo.InvariantCulture, out var parsed) &&
            parsed != Uuid.Empty)
        {
            try
            {
                await foreach (var record in factory.Services.GetRequiredService<IEventStore>()
                                   .ReadAsync(new TenantInvitation(tenant, parsed).Stream, 0))
                    facts.Add(record.Event);
            }
            catch (InvalidOperationException)
            {
                // An unknown stream reads as empty.
            }
        }

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.NoContent ? 1 : 0, facts.Count);
    }

    [Theory]
    [InlineData("invitations")]
    [InlineData("member-invitations")]
    [InlineData("guest-invitations")]
    public async Task ShouldRetireTheOldInvitationCreationRoutesGivenIdentifiedInvitationCommands(string collection)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var path = $"/api/v1/tenants/{Uuid.CreateVersion4()}/{collection}";

        // Act
        using var post = await client.PostAsJsonAsync(path, new { email_address = "retired@example.com" });

        // Assert
        Assert.True(post.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, post.StatusCode.ToString());
    }

    [Fact]
    public async Task ShouldRetireTheOldAcceptanceRouteGivenRouteBoundInvitationId()
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);

        // Act
        using var post = await client.PostAsJsonAsync($"/api/v1/tenants/{Uuid.CreateVersion4()}/invitations/acceptance",
            new { email_address = "retired@example.com", token = "token" });
        using var put = await client.PutAsJsonAsync($"/api/v1/tenants/{Uuid.CreateVersion4()}/invitations/acceptance",
            new { email_address = "retired@example.com", token = "token" });

        // Assert
        Assert.True(post.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, post.StatusCode.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ShouldRejectMalformedRouteIdentityGivenAcceptance(string invitationId)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);

        // Act
        using var response = await client.PutAsJsonAsync(
            $"/api/v1/tenants/{Uuid.CreateVersion4()}/invitations/{invitationId}/acceptance",
            new { email_address = "route@example.com", token = "token" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("bdgrz.tenant-member.invite", null)]
    [InlineData("bdgrz.tenant-member.invite", "malformed")]
    [InlineData("bdgrz.organization-member.invite", null)]
    [InlineData("bdgrz.organization-member.invite", "00000000-0000-0000-0000-000000000000")]
    [InlineData("bdgrz.tenant.guest-consultant.invite", null)]
    [InlineData("bdgrz.tenant.guest-consultant.invite", "malformed")]
    public async Task ShouldRejectMissingOrInvalidTypedIdentityGivenMcpInput(string tool, string? invitationId)
    {
        // Arrange
        await using var factory = Factory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));
        var arguments = new Dictionary<string, object?>
        {
            ["tenant_id"] = Uuid.CreateVersion4().ToString(),
            ["email_address"] = "mcp@example.com",
            ["affiliation"] = "client_personnel",
            ["built_in_role"] = BuiltInRbac.ComplianceManagementRole,
            ["program_ids"] = new[] { Uuid.CreateVersion4().ToString() },
            ["expires_on"] = "2027-01-31",
        };
        if (invitationId is not null)
            arguments["invitation_id"] = invitationId;

        // Act
        var tools = await scenario.ListTools();
        var result = await scenario.When(tool, arguments);

        // Assert
        var listed = Assert.Single(tools, item => item.Name == tool);
        Assert.Contains("invitation_id", listed.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.True(result.IsError);
    }

    static WebApplicationFactory<Program> Factory() => ProviderHttpMcpTests.CreateFactory()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPermissionAuthorizer>();
            services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        }));

    static async Task LoginAsync(HttpClient client)
    {
        using var body = new StringContent("{\"email_address\":\"invitation-transport@example.com\"}", Encoding.UTF8,
            "application/json");
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
