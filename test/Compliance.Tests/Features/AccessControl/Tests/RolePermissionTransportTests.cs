using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

[Trait("Category", "WebIntegration")]
public sealed class RolePermissionTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldBindNaturalRelationshipIdentityBeforeAuthorizationGivenHttpCommand(bool remove)
    {
        // Arrange
        var calls = new List<BusCall>();
        await using var factory = Factory(calls);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        using var request = new HttpRequestMessage(remove ? HttpMethod.Delete : HttpMethod.Put,
            $"/v1/roles/{role}/permissions/controls.read")
        {
            Content = new StringContent("{\"expected_revision\":1}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Tenant-Id", tenant.ToString());

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var dispatched = calls.Where(call => call.Request is AssignRolePermission or RemoveRolePermission).ToArray();
        // The catalog remains system-owned. Binding reaches the real authorizer, which denies this member.
        var call = Assert.Single(dispatched);
        if (remove)
        {
            var command = Assert.IsType<RemoveRolePermission>(call.Request);
            Assert.Equal(tenant, command.TenantId);
            Assert.Equal(role, command.RoleId);
            Assert.Equal("controls.read", command.Permission);
            Assert.Equal(1UL, command.ExpectedRevision);
            Assert.Equal(((IIdentifiedRequest)command).RequestId, call.Context.RequestId);
        }
        else
        {
            var command = Assert.IsType<AssignRolePermission>(call.Request);
            Assert.Equal(tenant, command.TenantId);
            Assert.Equal(role, command.RoleId);
            Assert.Equal("controls.read", command.Permission);
            Assert.Equal(1UL, command.ExpectedRevision);
            Assert.Equal(((IIdentifiedRequest)command).RequestId, call.Context.RequestId);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldExposeOnlyNaturalRelationshipIdentityGivenMcpCommand(bool remove)
    {
        // Arrange
        var calls = new List<BusCall>();
        await using var factory = Factory(calls);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        await using var scenario = await McpScenario.ConnectAsync(client, new Uri(client.BaseAddress!, "/mcp"));
        var name = remove ? "bdgrz.rbac.role-permission.remove" : "bdgrz.rbac.role-permission.assign";
        var tools = await scenario.ListTools();
        var tool = Assert.Single(tools, item => item.Name == name);
        var arguments = new Dictionary<string, object?>
        {
            ["tenant_id"] = Uuid.CreateVersion4().ToString(),
            ["role_id"] = Uuid.CreateVersion4().ToString(),
            ["permission"] = "controls.read",
            ["expected_revision"] = 1,
        };

        // Act
        var bound = await scenario.When(name, arguments);

        // Assert
        Assert.DoesNotContain("command_id", tool.InputSchema.GetProperty("properties").EnumerateObject().Select(item => item.Name));
        Assert.DoesNotContain("assignment_id", tool.InputSchema.GetProperty("properties").EnumerateObject().Select(item => item.Name));
        Assert.True(bound.IsError);
        Assert.Equal("Forbidden", bound.Error?.GetProperty("kind").GetString());
        var call = Assert.Single(calls, item => item.Request is AssignRolePermission or RemoveRolePermission);
        Assert.Equal(((IIdentifiedRequest)call.Request).RequestId, call.Context.RequestId);
    }

    [Fact]
    public async Task ShouldPreserveAndValidateTypedIntentGivenAQueueEnvelope()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        var options = factory.Services.GetRequiredKeyedService<JsonSerializerOptions>(PortiaServiceKeys.Json);
        var serializer = new JsonRequestSerializer([
            new RequestTransportRegistration(typeof(RemoveRolePermission), [RequestTransportId.Queue],
                new RequestRouteAttribute("bdgrz", "rbac", "permissions", "remove"),
                new DiscriminatorAttribute("bdgrz.rbac.role-permission.remove", 2)),
        ], options);
        var command = new RemoveRolePermission(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "controls.read", 7);
        var metadata = RequestMetadata.FromRequest(command);

        // Act
        var bytes = serializer.Serialize(command, null, metadata, null);
        var envelope = serializer.DeserializeEnvelope(bytes);
        var tampered = JsonNode.Parse(bytes.Span)!;
        var payload = tampered["payload"]!.AsObject();
        payload["expected_revision"] = 8;

        // Assert
        Assert.Equal(command, Assert.IsType<RemoveRolePermission>(envelope.Request));
        Assert.Equal(((IIdentifiedRequest)command).RequestId, envelope.Metadata.RequestId);
        Assert.Throws<InvalidOperationException>(() => serializer.DeserializeEnvelope(
            Encoding.UTF8.GetBytes(tampered.ToJsonString())));
    }

    static WebApplicationFactory<Program> Factory(List<BusCall> calls) =>
        ProviderHttpMcpTests.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            var bus = services.Last(descriptor => descriptor.ServiceType == typeof(IRequestBus));
            services.RemoveAll<IRequestBus>();
            services.Add(new ServiceDescriptor(typeof(IRequestBus), provider =>
                new ObservedBus((IRequestBus)(bus.ImplementationInstance ?? bus.ImplementationFactory?.Invoke(provider) ??
                    ActivatorUtilities.CreateInstance(provider, bus.ImplementationType!)), calls), bus.Lifetime));
        }));

    static async Task LoginAsync(HttpClient client)
    {
        using var body = new StringContent("{\"email_address\":\"permission-transport@example.com\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/v1/developer-user-sessions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    sealed record BusCall(IRequestBase Request, RequestDispatchContext Context);

    sealed class ObservedBus(IRequestBus inner, List<BusCall> calls) : IRequestBus
    {
        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
            inner.CreateContext(actor, metadata);
        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.AuthorizeAsync(request, context, ct);
        public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
            CancellationToken ct = default)
        {
            calls.Add(new(request, context));
            return inner.DispatchAsync(request, context, ct);
        }
        public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.DispatchAsync(request, context, ct);
        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.DispatchStreamAsync(request, context, ct);
    }
}
