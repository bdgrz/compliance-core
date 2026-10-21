using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Seals a completed finding migration at its previously committed source fence.</summary>
[Discriminator("bdgrz.remediation.finding-migration.completed", 1)]
public sealed record FindingMigrationCompleted(Uuid TenantId, Uuid ProgramId, Uuid MigrationId,
    Uuid FenceEventId, ulong FenceMarkerResourceOffset, ulong ImportedFindingEventCount,
    string SourceDigest, ActorReference CompletedBy, DateTimeOffset CompletedAt)
    : DomainEvent;
