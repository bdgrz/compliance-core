using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Defines a built-in role within a tenant. <paramref name="RoleId" /> is the immutable creation identity: an exact
///     retry replays the original acceptance and the same identity with a different name conflicts.
/// </summary>
[Discriminator("bdgrz.rbac.role.define", 1)]
public sealed record DefineRole(Uuid TenantId, Uuid RoleId, string Name)
    : IRequest, IRbacManagementRequest, ICallable;
