using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>One retained storage disposition attempt and its exact result.</summary>
public sealed record ArtifactDispositionAttemptView(
    int Number,
    string Status,
    ActorReference RequestedBy,
    DateTimeOffset RequestedAt,
    ulong SourcePosition,
    string? Outcome,
    string? FailureCode,
    ActorReference? CompletedBy,
    DateTimeOffset? CompletedAt);
