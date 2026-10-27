using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Events;

[Discriminator("bdgrz.tenant.suspended", 1)]
public sealed record TenantSuspended(Uuid TenantId, Uuid OperatorUserId) : DomainEvent
{
    public string? Reason { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
