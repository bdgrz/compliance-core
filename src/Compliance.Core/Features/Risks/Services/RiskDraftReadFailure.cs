using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Risks.Services;

public static class RiskDraftReadFailure
{
    public static RequestError Of(ImmutableContentIntegrityException failure) => new(RequestErrorKind.Conflict,
        "The risk draft's exact immutable source cannot be verified.", failure is ImmutableContentUnavailableException);
}
