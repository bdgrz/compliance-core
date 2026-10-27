using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

[Discriminator("bdgrz.independence.service-reevaluated", 1)]
public sealed record ServiceIndependenceReevaluated(Uuid TenantId, long ExpectedSequence,
    ServiceIndependenceReevaluationView Reevaluation) : DomainEvent;
