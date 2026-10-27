using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

/// <summary>Reads one role by id.</summary>
[Discriminator("bdgrz.rbac.role.get", 1)]
public sealed record GetRole(Uuid TenantId, Uuid RoleId) : IRequest<RoleView>, ITenantAccessRequest, ICallable;
