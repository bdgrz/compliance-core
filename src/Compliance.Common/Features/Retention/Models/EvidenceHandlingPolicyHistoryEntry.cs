using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>An immutable, attributed handling-class decision.</summary>
public sealed record EvidenceHandlingPolicyHistoryEntry(
    long Revision,
    string HandlingClass,
    long PolicyVersion,
    string Rationale,
    ActorReference AssignedBy,
    DateTimeOffset AssignedAt);
