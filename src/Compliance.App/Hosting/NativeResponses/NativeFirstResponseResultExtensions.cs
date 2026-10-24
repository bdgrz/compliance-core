using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>Composes one Portia result callback with exact native bytes and preserves existing callback failures.</summary>
public static class NativeFirstResponseResultExtensions
{
    public static PortiaEndpointConfiguration<TRequest> OnNativeResult<TRequest>(this PortiaEndpointConfiguration<TRequest> config)
    {
        if (NativeFirstResponseContracts.ImmediateUnit(typeof(TRequest).Name))
            config.Produces<UnitResult>(StatusCodes.Status200OK);
        return config.OnNativeResult(static (_, _) => null);
    }
    public static PortiaEndpointConfiguration<TRequest, TOut> OnNativeResult<TRequest, TOut>(this PortiaEndpointConfiguration<TRequest, TOut> config)
    {
        if (NativeFirstResponseContracts.Pending(typeof(TRequest).Name) && typeof(TOut) == typeof(OperationAcceptedResponse))
            config.Produces<OperationAcceptedResponse>(StatusCodes.Status202Accepted);
        return config.OnNativeResult(static (_, _) => null);
    }
    public static PortiaEndpointConfiguration<TRequest> OnNativeResult<TRequest>(this PortiaEndpointConfiguration<TRequest> config,
        Func<HttpContext, Result, IResult?> callback) => config.OnNativeResult((http, result, _) => ValueTask.FromResult(callback(http, result)));
    public static PortiaEndpointConfiguration<TRequest, TOut> OnNativeResult<TRequest, TOut>(this PortiaEndpointConfiguration<TRequest, TOut> config,
        Func<HttpContext, Result<TOut>, IResult?> callback) => config.OnNativeResult((http, result, _) => ValueTask.FromResult(callback(http, result)));
    public static PortiaEndpointConfiguration<TRequest> OnNativeResult<TRequest>(this PortiaEndpointConfiguration<TRequest> config,
        Func<HttpContext, Result, CancellationToken, ValueTask<IResult?>> callback) => config.OnResult(async (http, result, ct) =>
    {
        var custom = await callback(http, result, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return IsError(custom) ? custom : result.ToHttpResult();
        if (custom is not null && !IsKnownSuccess(custom))
            return custom;
        var native = await NativeFirstResponseHttp.ReadAsync(http, ct).ConfigureAwait(false);
        return native ?? custom ?? (NativeFirstResponseContracts.ImmediateUnit(typeof(TRequest).Name)
            ? Results.Json(new UnitResult(), ComplianceCoreJsonContext.Default.UnitResult) : result.ToHttpResult());
    });
    public static PortiaEndpointConfiguration<TRequest, TOut> OnNativeResult<TRequest, TOut>(this PortiaEndpointConfiguration<TRequest, TOut> config,
        Func<HttpContext, Result<TOut>, CancellationToken, ValueTask<IResult?>> callback) => config.OnResult(async (http, result, ct) =>
    {
        var custom = await callback(http, result, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return IsError(custom) ? custom : Result.Failure(result.Error).ToHttpResult();
        if (custom is not null && !IsKnownSuccess(custom))
            return custom;
        var native = await NativeFirstResponseHttp.ReadAsync(http, ct).ConfigureAwait(false);
        if (native is not null || custom is not null)
            return native ?? custom;
        var options = http.RequestServices.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        var typeInfo = (JsonTypeInfo<TOut>)options.GetTypeInfo(typeof(TOut));
        if (NativeFirstResponseContracts.Pending(typeof(TRequest).Name) && result.Value is OperationAcceptedResponse pending)
        {
            http.Response.Headers.Location = pending.StatusUrl;
            return Results.Json(result.Value, typeInfo, statusCode: StatusCodes.Status202Accepted);
        }
        return result.ToHttpResult(typeInfo);
    });
    static bool IsError(IResult? result) => result is IStatusCodeHttpResult { StatusCode: >= 400 };
    static bool IsKnownSuccess(IResult result) => result is IStatusCodeHttpResult { StatusCode: null or >= 200 and < 400 };
}
