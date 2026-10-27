using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.Models;

public sealed record TenantProvisioningStatusView(Uuid TenantId, string Status, string? FailureCode);
