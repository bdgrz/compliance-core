using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Events;

[Discriminator("bdgrz.workforce.source.reconciled", 1)]
public sealed record WorkforceSourceReconciled(Uuid TenantId, Uuid ObservationId, long Revision,
    WorkforceSourceDecision Decision) : DomainEvent;
