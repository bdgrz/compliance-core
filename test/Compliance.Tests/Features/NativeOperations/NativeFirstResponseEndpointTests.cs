using System.Net;
using System.Text.Json;
using Bdgrz.Compliance.Hosting.NativeResponses;
using Bdgrz.Compliance.Tests.Features.Providers;
using Cntryl.Portia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

[Trait("Category", "WebIntegration")]
public sealed class NativeFirstResponseEndpointTests
{
    [Fact]
    public async Task ShouldRequireNativeResultContractForEveryMutationGivenProductionEndpointInventory()
    {
        // Arrange
        await using var factory = ProviderHttpMcpTests.CreateFactory();
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var mutations = endpoints.Where(endpoint => endpoint.Metadata.GetMetadata<PortiaOpenApiOperation>() is not null &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Any(static method => method is "POST" or "PUT" or "DELETE" or "PATCH") == true).ToArray();

        // Act
        var failure = Record.Exception(() => NativeFirstResponseEndpointValidation.Validate(endpoints));
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.NotEmpty(mutations);
        Assert.Null(failure);
        Assert.All(mutations, endpoint => Assert.NotNull(endpoint.Metadata.GetMetadata<NativeFirstResponseEndpoint>()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grant = document.RootElement.GetProperty("paths").GetProperty("/api/v1/platform/operator-grants/{command_id}").GetProperty("put").GetProperty("responses");
        Assert.True(grant.TryGetProperty("200", out var unit));
        Assert.True(unit.GetProperty("content").TryGetProperty("application/json", out _));
        Assert.False(grant.TryGetProperty("204", out _));
    }

    [Fact]
    public void ShouldRejectUnadaptedFutureMutationGivenMissingNativeEndpointContract()
    {
        // Arrange
        var endpoint = new Endpoint(null, new EndpointMetadataCollection(new HttpMethodMetadata(["PUT"]),
            new PortiaOpenApiOperation("future-mutation", null, true, false, false, false, [])), "future mutation");

        // Act
        var failure = Record.Exception(() => NativeFirstResponseEndpointValidation.Validate([endpoint]));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Contains("native first-response", failure.Message, StringComparison.Ordinal);
    }
}
