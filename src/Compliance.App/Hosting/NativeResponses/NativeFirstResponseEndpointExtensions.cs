using Microsoft.AspNetCore.Builder;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
namespace Bdgrz.Compliance.Hosting.NativeResponses;

public static class NativeFirstResponseEndpointExtensions
{
    public static IEndpointConventionBuilder WithNativeFirstResponse<TRequest>(this IEndpointConventionBuilder builder) =>
        builder.WithMetadata(new NativeFirstResponseEndpoint(typeof(TRequest)));
}
