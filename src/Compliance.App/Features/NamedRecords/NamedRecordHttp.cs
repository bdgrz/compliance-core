using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
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
