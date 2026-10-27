using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
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
