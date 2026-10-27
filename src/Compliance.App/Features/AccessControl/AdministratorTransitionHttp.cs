using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
