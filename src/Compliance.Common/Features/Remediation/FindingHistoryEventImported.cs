using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Copies one legacy program-stream event into its finding stream while retaining the source
///     event identity, physical offset, and aggregate version. It is atomically followed by the original typed fact, avoiding
///     a second copy of its payload and preserving every fact accepted by the source store.
/// </summary>
[Discriminator("bdgrz.finding.history-event-imported", 1)]
public sealed record FindingHistoryEventImported(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, Uuid SourceEventId, ulong SourceProgramStreamOffset,
    ulong SourceProgramStreamVersion, string SourceEventKind, string SourceEventSha256,
    DateTimeOffset SourceOccurredAt) : DomainEvent;
