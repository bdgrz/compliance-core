using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Deterministic member registrations dispatched by internal reactors, so replays reuse one logical id.</summary>
static class MemberRegistrationEffects
{
    /// <summary>The registration command a source event causes for one tenant user.</summary>
    public static RegisterMember Register(Uuid sourceEventId, Uuid tenantId, Uuid userId,
        string affiliation = "client_personnel", DateOnly? membershipExpiresOn = null) =>
        new(CommandId(sourceEventId, tenantId, userId), tenantId, userId, affiliation, membershipExpiresOn);

    /// <summary>The logical registration id, which is also the membership episode id.</summary>
    public static Uuid CommandId(Uuid sourceEventId, Uuid tenantId, Uuid userId) =>
        EffectIdentity.CommandId(sourceEventId, "member:register", $"{tenantId}:{userId}", null);
}
