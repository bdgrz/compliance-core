using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.acceptance.linked", 2)]
public sealed record FindingAcceptanceLinkedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, FindingAcceptanceView Acceptance) : DomainEvent;
