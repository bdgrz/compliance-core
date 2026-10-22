using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.corrective_action.added", 2)]
public sealed record CorrectiveActionAddedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, CorrectiveActionView Action) : DomainEvent;
