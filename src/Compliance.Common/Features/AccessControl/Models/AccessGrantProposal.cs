using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record AccessGrantProposal(
    AccessGrantPrincipal Principal,
    Uuid RoleId,
    AccessGrantScope Scope,
    AccessGrantSource Source,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil);
