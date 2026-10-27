using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>
///     The authored resolution decision frozen at admission: the normalized explanation, the exact response selection
///     (equal to every response admitted on the request) and the attributable resolver.
/// </summary>
public sealed record AuditorResolutionDecision(Uuid DecisionId, Uuid ProgramId, string Resolution,
    IReadOnlyList<Uuid> ResponseIds, Uuid ResolverMemberId, ActorReference ResolvedBy, DateTimeOffset DecidedAt);
