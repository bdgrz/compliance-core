using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Projections;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
namespace Bdgrz.Compliance.Features.AuditorRequests.Processes;

/// <summary>
///     A verified resolution: the authoritative request, its accepted resolution fact, the frozen resolution intent and
///     the authored decision. Exactly one of <paramref name="Error" /> and the verified parts is set.
/// </summary>
public sealed record AuditorRequestResolution(RequestError? Error, AuditorRequest? Request, ClosureIntent? Intent,
    AuditorResolutionDecision? Decision, Uuid TenantId, AuditorRequestHistorySnapshot? History = null)
{
    /// <summary>A failed verification.</summary>
    public static AuditorRequestResolution Failed(RequestError error) => new(error, null, null, null, Uuid.Empty);
}
