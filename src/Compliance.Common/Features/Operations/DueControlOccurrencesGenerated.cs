using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records a deterministic, replay-safe batch of due Type II occurrence identities.</summary>
[Discriminator("bdgrz.operations.due-control-occurrences-generated", 2)]
public sealed record DueControlOccurrencesGenerated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    string GenerationKey,
    DateOnly ThroughDate,
    IReadOnlyList<Uuid> OccurrenceIds,
    Uuid? ActorMemberId,
    string ActorDisplay,
    DateTimeOffset OccurredAt) : DomainEvent;
