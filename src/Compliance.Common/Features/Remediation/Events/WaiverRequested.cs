using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

[Discriminator("bdgrz.waiver.requested", 1)]
public sealed record WaiverRequested(Uuid TenantId, Uuid WaiverId, string Rule,
    IReadOnlyList<WaiverScopeRecord> Scope, string Rationale, string CompensatingAction,
    DateTimeOffset ExpiresAt, ActorReference RequestedBy, DateTimeOffset RequestedAt) : DomainEvent;
