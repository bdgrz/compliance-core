using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     The authored completion review frozen at admission: the program and risk of the action, the outcome, the
///     normalized rationale, the reviewer and, when the reviewer needed one, the waiver identity with the exact
///     separation-of-duties waiver proof that authorizes it. The decision id is the root operation id.
/// </summary>
public sealed record RiskActionReviewDecision(Uuid DecisionId, Uuid ProgramId, Uuid RiskId, string Outcome,
    string Rationale, Uuid ReviewerMemberId, ActorReference Reviewer, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId, ImmutableContentRef? WaiverProof);
