using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Defines a team within a tenant. <paramref name="TeamId" /> is the immutable creation identity: an exact retry
///     replays the original acceptance and the same identity with a different name conflicts.
/// </summary>
[Discriminator("bdgrz.rbac.team.define", 1)]
public sealed record DefineTeam(Uuid TenantId, Uuid TeamId, string Name) : IRequest, IClientRbacMutationRequest, ICallable;
