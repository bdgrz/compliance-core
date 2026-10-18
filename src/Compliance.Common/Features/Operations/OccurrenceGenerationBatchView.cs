using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>A replay-safe generation request and the immutable expected occurrences it emitted.</summary>
public sealed record OccurrenceGenerationBatchView(
    long Revision,
    string GenerationKey,
    DateOnly ThroughDate,
    IReadOnlyList<Uuid> OccurrenceIds,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset GeneratedAt);
