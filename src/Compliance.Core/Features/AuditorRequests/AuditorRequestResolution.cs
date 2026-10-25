using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AuditorRequests;

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
