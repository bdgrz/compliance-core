using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Immutable authority for removing relationships from one terminated membership episode.</summary>
public sealed record MembershipTerminationProof(Uuid TenantId, Uuid MemberId, Uuid UserId,
    Uuid MembershipEpisodeId, Uuid EventId, ulong Revision, ulong Position, string Sha256,
    Uuid ActorMemberId, string ActorDisplay);
