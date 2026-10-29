using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Retention.Models;

/// <summary>A reviewed classification bound to one exact, immutable artifact identity.</summary>
public sealed record EvidenceHandlingPolicyView(
    Uuid TenantId,
    Uuid ArtifactId,
    ArtifactRetentionSource Source,
    ulong SourcePosition,
    long ContentLength,
    string SourceState,
    string SourceHandlingClass,
    string HandlingClass,
    long PolicyVersion,
    long Revision,
    string Rationale,
    ActorReference AssignedBy,
    DateTimeOffset AssignedAt,
    IReadOnlyList<EvidenceHandlingPolicyHistoryEntry> History);
