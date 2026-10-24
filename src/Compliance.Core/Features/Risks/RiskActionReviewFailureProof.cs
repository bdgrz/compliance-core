using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>The permanent reason a pending review can never decide the completion, and the evidence request that caused it.</summary>
public sealed record RiskActionReviewFailureProof(Uuid ProgramId, Uuid RiskId, Uuid ActionId, string Reason,
    Uuid? EvidenceRequestId);
