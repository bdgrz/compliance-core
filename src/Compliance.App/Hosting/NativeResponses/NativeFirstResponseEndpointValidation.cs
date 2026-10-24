using Cntryl.Portia;
using Microsoft.AspNetCore.Routing;

namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>Rejects a generated mutation endpoint that omits the native transport contract marker.</summary>
public sealed class NativeFirstResponseEndpointValidation(IEnumerable<EndpointDataSource> sources) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Validate(sources.SelectMany(static source => source.Endpoints));
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public static void Validate(IEnumerable<Endpoint> endpoints)
    {
        foreach (var endpoint in endpoints)
        {
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            if (endpoint.Metadata.GetMetadata<PortiaOpenApiOperation>() is null || methods is null ||
                !methods.Any(static method => method is "POST" or "PUT" or "PATCH" or "DELETE"))
                continue;
            if (endpoint.Metadata.GetMetadata<NativeFirstResponseEndpoint>() is null)
                throw new InvalidOperationException("Every generated mutation endpoint must install its native first-response result contract: " + endpoint.DisplayName);
        }
    }
}
