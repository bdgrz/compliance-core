using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.revised", 2)]
public sealed record FindingRevisedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId, long Revision,
    string Severity, Uuid OwnerMemberId, DateOnly DueOn, string AffectedScope, string? RootCause,
    string Reason, ActorReference RevisedBy, DateTimeOffset RevisedAt) : DomainEvent;
