using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Immutable current decision authority and history commitments, without historical narrative or decision bodies.</summary>
public sealed record SystemDescriptionCurrentAuthority(ImmutableContentRef CurrentVersionContent,
    IReadOnlyList<SystemDescriptionVersionToken> Versions, IReadOnlyList<string> ReviewHashes,
    IReadOnlyList<string> ApprovalHashes, IReadOnlyList<string> NoChangeDecisionHashes, IReadOnlyList<string> BindingHashes,
    Uuid? LatestApprovedVersionId, IReadOnlyList<SystemDescriptionReviewView> CurrentReviews,
    SystemDescriptionApprovalView? CurrentApproval, IReadOnlyList<SystemDescriptionNoChangeDecisionView> CurrentNoChangeDecisions,
    IReadOnlyList<SystemDescriptionBindingView> CurrentBindings);
