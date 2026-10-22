using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Tests.Features.Providers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

[Trait("Category", "WebIntegration")]
public sealed class RolePermissionTransportTests
{
    const string OperationId = "b4eb443f-94d4-4ca6-aec8-06d7946ff1da";
    const string AssignmentId = "dc97120d-631c-4aa7-ae05-ecf9e5d18791";

    [Theory]
    [InlineData(false, "", "", HttpStatusCode.NotFound)]
    [InlineData(false, "malformed", "", HttpStatusCode.BadRequest)]
    [InlineData(false, "00000000-0000-0000-0000-000000000000", "", HttpStatusCode.BadRequest)]
    [InlineData(false, OperationId, "", HttpStatusCode.Forbidden)]
    [InlineData(false, OperationId, ",\"command_id\":\"" + OperationId + "\"", HttpStatusCode.Forbidden)]
    [InlineData(false, OperationId, ",\"command_id\":\"" + AssignmentId + "\"", HttpStatusCode.BadRequest)]
    [InlineData(false, OperationId, ",\"command_id\":null", HttpStatusCode.BadRequest)]
    [InlineData(false, OperationId, ",\"command_id\":\"malformed\"", HttpStatusCode.BadRequest)]
    [InlineData(true, OperationId, "", HttpStatusCode.Forbidden)]
    [InlineData(true, OperationId, ",\"command_id\":\"" + AssignmentId + "\"", HttpStatusCode.BadRequest)]
    public async Task ShouldBindRequiredOperationIdentityBeforeDispatchGivenHttpCommand(
        bool remove, string operationId, string duplicate, HttpStatusCode expected)
    {
        // Arrange
        var calls = new List<BusCall>();
        await using var factory = Factory(calls);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var tenant = Uuid.CreateVersion4();
        var role = Uuid.CreateVersion4();
        using var request = new HttpRequestMessage(remove ? HttpMethod.Delete : HttpMethod.Put,
            $"/api/v1/tenants/{tenant}/roles/{role}/permissions/controls.read/{(remove ? "removals" : "assignments")}/{operationId}" +
            (remove ? $"?expected_revision=1&assignment_id={AssignmentId}" : ""));
        request.Content = new StringContent("{\"expected_revision\":1,\"assignment_id\":\"" + AssignmentId + "\"" + duplicate + "}",
            Encoding.UTF8, "application/json");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(expected, response.StatusCode);
        var dispatched = calls.Where(call => call.Request is AssignRolePermission or RemoveRolePermission).ToArray();
        if (expected != HttpStatusCode.Forbidden)
        {
            Assert.Empty(dispatched);
            return;
        }
        // The catalog remains system-owned. Binding reaches the real authorizer, which denies this member.
        var call = Assert.Single(dispatched);
        Assert.Equal(OperationId, call.Context.RequestId.ToString());
        if (remove)
        {
            var command = Assert.IsType<RemoveRolePermission>(call.Request);
            Assert.Equal(tenant, command.TenantId);
            Assert.Equal(role, command.RoleId);
            Assert.Equal(OperationId, command.CommandId.ToString());
            Assert.Equal(AssignmentId, command.AssignmentId.ToString());
            Assert.Equal(1UL, command.ExpectedRevision);
        }
        else
        {
            var command = Assert.IsType<AssignRolePermission>(call.Request);
            Assert.Equal(tenant, command.TenantId);
            Assert.Equal(role, command.RoleId);
            Assert.Equal(OperationId, command.CommandId.ToString());
            Assert.Equal(1UL, command.ExpectedRevision);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRequireTheOrdinaryTypedIdentityGivenMcpCommand(bool remove)
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
        if (remove)
            arguments["assignment_id"] = AssignmentId;

        // Act
        var missing = await scenario.When(name, arguments);
        arguments["command_id"] = OperationId;
        var bound = await scenario.When(name, arguments);

        // Assert
        Assert.Contains("command_id", tool.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.True(missing.IsError);
        Assert.True(bound.IsError);
        Assert.Equal("Forbidden", bound.Error?.GetProperty("kind").GetString());
        var call = Assert.Single(calls, item => item.Request is AssignRolePermission or RemoveRolePermission);
        Assert.Equal(OperationId, ((IIdentifiedRequest)call.Request).RequestId.ToString());
        Assert.Equal(OperationId, call.Context.RequestId.ToString());
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
                new DiscriminatorAttribute("bdgrz.rbac.role-permission.remove", 1)),
        ], options);
        var command = new RemoveRolePermission(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "controls.read",
            Uuid.CreateVersion4(), 7, Uuid.CreateVersion4());
        var metadata = RequestMetadata.FromRequest(command);

        // Act
        var bytes = serializer.Serialize(command, null, metadata, null);
        var envelope = serializer.DeserializeEnvelope(bytes);
        var tampered = JsonNode.Parse(bytes.Span)!;
        var payload = tampered["payload"]!.AsObject();
        var key = Assert.Single(payload.Select(pair => pair.Key), name => name is "command_id" or "commandId" or "CommandId");
        payload[key] = Uuid.CreateVersion4().ToString();

        // Assert
        Assert.Equal(command, Assert.IsType<RemoveRolePermission>(envelope.Request));
        Assert.Equal(command.CommandId, envelope.Metadata.RequestId);
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
        using var response = await client.PostAsync("/api/v1/developer-user-sessions", body);
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
