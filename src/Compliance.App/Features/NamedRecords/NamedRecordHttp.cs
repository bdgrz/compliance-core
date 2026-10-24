using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NamedRecords;

/// <summary>Maps the frozen acknowledgment of a named record change to its HTTP status.</summary>
static class NamedRecordHttp
{
    /// <summary>
    ///     An accepted change answers 202 with the frozen <see cref="OperationAcceptedResponse" /> on every exact retry;
    ///     every failure keeps Portia's default mapping. The status resource the body names is not mapped yet.
    /// </summary>
    public static IResult? Result(Result<OperationAcceptedResponse> result) =>
        result.IsSuccess
            ? Results.Json(result.Value, ComplianceCoreJsonContext.Default.OperationAcceptedResponse,
                statusCode: StatusCodes.Status202Accepted)
            : null;
}
