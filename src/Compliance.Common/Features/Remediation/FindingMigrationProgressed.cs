using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Advances one physical source offset after its fact import or audit skip is durable.</summary>
[Discriminator("bdgrz.remediation.finding-migration.progressed", 1)]
public sealed record FindingMigrationProgressed(Uuid TenantId, Uuid ProgramId, Uuid MigrationId,
    ulong SourceOffset, Uuid SourceEventId, ulong SourceProgramVersion,
    DateTimeOffset SourceOccurredAt, bool SourceIsAudit, Uuid? FindingId,
    string? SourceEventKind, string? SourceEventSha256, ulong NextSourceOffset,
    ulong ImportedFindingEventCount, string SourceDigest, DateTimeOffset ProgressedAt)
    : DomainEvent;
