using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     The canonical authored content of one admitted response draft: the normalized summary and evidence, whether the
///     request required an independent internal review, and the original author. The response owner never loads the
///     live request, so the review policy is frozen here at admission.
/// </summary>
public sealed record AuditorResponseDraftContent(Uuid ProgramId, Uuid RequestId, Uuid ResponseId, string Summary,
    IReadOnlyList<Uuid> EvidenceArtifactIds, bool RequiresInternalReview, Uuid AuthorMemberId, string AuthorDisplay,
    DateTimeOffset At);
