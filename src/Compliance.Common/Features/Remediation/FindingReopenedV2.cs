using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.reopened", 2)]
public sealed record FindingReopenedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, string Reason, ActorReference ReopenedBy, DateTimeOffset ReopenedAt)
    : DomainEvent;
