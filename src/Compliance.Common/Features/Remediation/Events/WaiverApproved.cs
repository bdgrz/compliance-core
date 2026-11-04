using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

[Discriminator("bdgrz.waiver.approved", 1)]
public sealed record WaiverApproved(Uuid CommandId, Uuid TenantId, Uuid WaiverId, long Revision,
    long OriginalExpectedRevision, string? Comments, ActorReference ApprovedBy, DateTimeOffset ApprovedAt) : DomainEvent;
