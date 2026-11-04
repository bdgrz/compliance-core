using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

[Discriminator("bdgrz.waiver.rejected", 1)]
public sealed record WaiverRejected(Uuid CommandId, Uuid TenantId, Uuid WaiverId, long Revision,
    long OriginalExpectedRevision, string? Comments, ActorReference ReviewedBy, DateTimeOffset ReviewedAt) : DomainEvent;
