using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>A retained immutable draft or submitted response version.</summary>
public sealed record AuditorResponseVersionView(
    int Version,
    string Summary,
    IReadOnlyList<Uuid> EvidenceArtifactIds,
    string Status,
    Uuid AuthorMemberId,
    string AuthorDisplay,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    string? SubmissionNote,
    AuditorResponseReviewView? Review);
