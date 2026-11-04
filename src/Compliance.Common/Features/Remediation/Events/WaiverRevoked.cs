using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

[Discriminator("bdgrz.waiver.revoked", 1)]
public sealed record WaiverRevoked(Uuid CommandId, Uuid TenantId, Uuid WaiverId, long Revision,
    long OriginalExpectedRevision, string Reason, ActorReference RevokedBy, DateTimeOffset RevokedAt) : DomainEvent;
