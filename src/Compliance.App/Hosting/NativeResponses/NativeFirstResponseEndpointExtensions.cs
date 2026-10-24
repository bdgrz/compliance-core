using Microsoft.AspNetCore.Builder;

namespace Bdgrz.Compliance.Hosting.NativeResponses;

public static class NativeFirstResponseEndpointExtensions
{
    public static IEndpointConventionBuilder WithNativeFirstResponse<TRequest>(this IEndpointConventionBuilder builder) =>
        builder.WithMetadata(new NativeFirstResponseEndpoint(typeof(TRequest)));
}
