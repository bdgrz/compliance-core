using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Maps the frozen identity recovery acknowledgment to its HTTP status.</summary>
static class IdentityRecoveryHttp
{
    /// <summary>
    ///     An accepted recovery answers 202 with the fixed acknowledgment, on the first request and on every exact
    ///     retry; every failure keeps Portia's default mapping.
    /// </summary>
    public static IResult? Result(Result<OperationAcceptedResponse> result) =>
        result.IsSuccess
            ? Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                statusCode: StatusCodes.Status202Accepted)
            : null;
}
