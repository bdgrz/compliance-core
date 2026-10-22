using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Captures the exact committed source fence before importing finding history.</summary>
[Discriminator("bdgrz.remediation.finding-migration.started", 1)]
public sealed record FindingMigrationStarted(Uuid TenantId, Uuid ProgramId, Uuid MigrationId,
    Uuid FenceEventId, ulong PreFenceProgramVersion, ulong FenceMarkerResourceOffset,
    ulong FenceMarkerProgramVersion, ActorReference StartedBy, DateTimeOffset StartedAt)
    : DomainEvent;
