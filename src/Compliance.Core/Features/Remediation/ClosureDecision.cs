using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     The authored closure decision frozen at admission: the reviewer, the normalized rationales and the complete
///     resolution evidence. The waiver identity is kept for audit; the proof that authorizes it is in the intent.
/// </summary>
public sealed record ClosureDecision(Uuid DecisionId, Uuid ProgramId, string VerificationRationale,
    IReadOnlyList<EvidenceReference> ResolutionEvidence, string Rationale, Uuid CloserMemberId, ActorReference ClosedBy,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId);
