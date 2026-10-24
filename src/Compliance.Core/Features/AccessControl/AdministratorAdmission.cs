using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One route-determined administrator admission: the authored command, the resolved member episode and the
///     source owner it will mutate. <paramref name="Bind" /> runs the source preflight against current guard state
///     and returns the revisions the durable intent pins.
/// </summary>
public sealed record AdministratorAdmission(Uuid CommandId, Uuid TenantId, string CommandKind, ActorReference Actor,
    string AuthoredIntentSha256, DateTimeOffset AcceptedAt, Uuid MemberId, Uuid MemberEpisodeId,
    EventStreamAddress SourceOwner, Uuid? AssignmentId, long AuthoredRevision,
    Func<TenantManagerGuard, Result<AdministratorSourceBinding>> Bind);
