using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.observation.resolved", 1)]
public sealed record WorkforceObservationResolved(Uuid TenantId, Uuid ObservationId,
    string Resolution, string Note, ActorReference Actor, DateTimeOffset ResolvedAt) : DomainEvent;
