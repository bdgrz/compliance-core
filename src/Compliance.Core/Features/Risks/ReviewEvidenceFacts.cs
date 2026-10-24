using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     What a review reads from one evidence request: whether it is fulfilled and by which artifact. These are the
///     required facts of the review use and are verified content. <paramref name="Refusal" /> is the permanent reason the
///     request cannot be held for this review, or null.
/// </summary>
public sealed record ReviewEvidenceFacts(Uuid EvidenceRequestId, bool Fulfilled, Uuid? ArtifactId, string? Refusal);
