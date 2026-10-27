using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention;

/// <summary>The current inspected artifact metadata used to fence a policy assignment.</summary>
public sealed record EvidenceHandlingPolicySourceSnapshot(
    ArtifactRetentionSource Source,
    ulong Position,
    long ContentLength,
    string SourceState,
    string SourceHandlingClass);
