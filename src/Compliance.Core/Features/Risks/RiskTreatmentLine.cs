using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     One treatment of a (risk, control) assertion line as the line's facts left it: the proposal, the review that
///     decided it and the retirement that withdrew it. The line identity is the logical id of the proposal.
/// </summary>
public sealed class RiskTreatmentLine(RiskControlTreatmentProposed proposed)
{
    /// <summary>The proposal fact.</summary>
    public RiskControlTreatmentProposed Proposed { get; } = proposed;

    /// <summary>The treatment identity: the logical id of the proposing operation.</summary>
    public Uuid TreatmentId => Proposed.Operation.LogicalId;

    /// <summary>The proposer member recorded with the proposal.</summary>
    public Uuid ProposerMemberId => Proposed.ProposerMemberId;

    /// <summary>The current status.</summary>
    public string Status { get; internal set; } = RiskStates.TreatmentProposed;

    /// <summary>The review fact, once decided.</summary>
    public RiskControlTreatmentReviewed? Reviewed { get; internal set; }

    /// <summary>The retirement fact, once withdrawn.</summary>
    public RiskControlTreatmentRetired? Retired { get; internal set; }
}
