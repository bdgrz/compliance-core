using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     One route-determined administrator admission: the authored command, the resolved member episode and the
///     source owner it will mutate. <paramref name="Bind" /> runs the source preflight against current guard state
///     and returns the revisions the durable intent pins. System current-episode inputs set
///     <paramref name="ResolveCurrentEpisode" /> so an accepted retry keeps its original episode.
/// </summary>
public sealed record AdministratorAdmission(Uuid CommandId, Uuid TenantId, string CommandKind, ActorReference Actor,
    string AuthoredIntentSha256, DateTimeOffset AcceptedAt, Uuid MemberId, Uuid MemberEpisodeId,
    EventStreamAddress SourceOwner, Uuid? AssignmentId, long AuthoredRevision,
    Func<TenantManagerGuard, Result<AdministratorSourceBinding>> Bind, bool ResolveCurrentEpisode = false);
