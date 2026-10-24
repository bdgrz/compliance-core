using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     The authored resolution decision frozen at admission: the normalized explanation, the exact response selection
///     (equal to every response admitted on the request) and the attributable resolver.
/// </summary>
public sealed record AuditorResolutionDecision(Uuid DecisionId, Uuid ProgramId, string Resolution,
    IReadOnlyList<Uuid> ResponseIds, Uuid ResolverMemberId, ActorReference ResolvedBy, DateTimeOffset DecidedAt);
