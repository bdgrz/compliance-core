using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.Contracts;

public interface ITenantInvitationDelivery
{
    ValueTask SendAsync(Uuid attemptId, Uuid tenantId, string emailAddress, string token,
        CancellationToken ct);
}
