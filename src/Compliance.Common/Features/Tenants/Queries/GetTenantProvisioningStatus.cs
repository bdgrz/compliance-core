using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Queries;

[Discriminator("bdgrz.tenant.provisioning_status.get", 1)]
public sealed record GetTenantProvisioningStatus(Uuid TenantId) : IRequest<TenantProvisioningStatusView>, ICallable;
