using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Defines a built-in role within a tenant. <paramref name="RoleId" /> is the immutable creation identity: an exact
///     retry replays the original acceptance and the same identity with a different name conflicts.
/// </summary>
[Discriminator("bdgrz.rbac.role.define", 1)]
public sealed record DefineRole(Uuid TenantId, Uuid RoleId, string Name)
    : IRequest, IRbacManagementRequest, ICallable;
