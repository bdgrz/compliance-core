using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention;

/// <summary>The exact current artifact, availability, and classification used for disclosure.</summary>
public sealed record ArtifactDisclosureSourceSnapshot(
    ArtifactRetentionSource Source,
    ulong Position,
    long ArtifactRevision,
    long ContentLength,
    string ArtifactState,
    string HandlingClass,
    long HandlingPolicyRevision);
