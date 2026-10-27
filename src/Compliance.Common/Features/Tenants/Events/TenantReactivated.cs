using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Events;

[Discriminator("bdgrz.tenant.reactivated", 1)]
public sealed record TenantReactivated(Uuid TenantId, Uuid OperatorUserId) : DomainEvent
{
    public string? Reason { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
