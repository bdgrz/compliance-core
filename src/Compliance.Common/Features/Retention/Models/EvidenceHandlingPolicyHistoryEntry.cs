using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Retention.Models;

/// <summary>An immutable, attributed handling-class decision.</summary>
public sealed record EvidenceHandlingPolicyHistoryEntry(
    long Revision,
    string HandlingClass,
    long PolicyVersion,
    string Rationale,
    ActorReference AssignedBy,
    DateTimeOffset AssignedAt);
