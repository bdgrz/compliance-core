using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Fences legacy program-ledger writes while finding streams are imported.</summary>
[Discriminator("bdgrz.remediation.partitioning.started", 1)]
public sealed record RemediationPartitioningStarted(Uuid TenantId, Uuid ProgramId,
    Uuid MigrationId, ulong PreFenceProgramVersion, ActorReference StartedBy,
    DateTimeOffset StartedAt) : DomainEvent;
