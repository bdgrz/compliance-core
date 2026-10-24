using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Maps the frozen administrator transition response to its HTTP status.</summary>
static class AdministratorTransitionHttp
{
    /// <summary>
    ///     An accepted transition answers 202 with the frozen response body while the process continues; a completed
    ///     one and every failure keep Portia's default mapping.
    /// </summary>
    public static IResult? Result(Result<AdministratorTransitionResponse> result) =>
        result.IsSuccess && result.Value!.State == AdministratorTransitionResponseState.Accepted
            ? Results.Json(result.Value, ComplianceCoreJsonContext.Default.AdministratorTransitionResponse,
                statusCode: StatusCodes.Status202Accepted)
            : null;
}
