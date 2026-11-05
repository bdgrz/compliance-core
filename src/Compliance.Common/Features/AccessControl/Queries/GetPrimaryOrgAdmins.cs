using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Queries;

[Discriminator("bdgrz.rbac.primary-org-admins.get", 1)]
public sealed record GetPrimaryOrgAdmins(Uuid TenantId) : IRequest<PrimaryOrgAdminsView>, IRbacManagementRequest, ICallable;
