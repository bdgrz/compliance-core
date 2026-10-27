using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Immutable authority for removing relationships from one terminated membership episode.</summary>
public sealed record MembershipTerminationProof(Uuid TenantId, Uuid MemberId, Uuid UserId,
    Uuid MembershipEpisodeId, Uuid EventId, ulong Revision, ulong Position, string Sha256,
    Uuid ActorMemberId, string ActorDisplay);
