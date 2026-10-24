using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     The governance of one risk as its own owners left it: the owner and triggers of its governance stream, the
///     treatment assertion lines and the treatment actions composed from their streams. This is a captured read-side
///     view and is never retained as command state.
/// </summary>
public sealed class RiskGovernanceRecord(long revision, RiskOwnerView? owner,
    IReadOnlyList<RiskControlTreatmentView> treatments, IReadOnlyList<RiskReassessmentTriggerView> triggers,
    IReadOnlyList<RiskTreatmentActionView> actions, IReadOnlyDictionary<Uuid, Uuid> submitters)
{
    /// <summary>An empty record for a risk with no governance facts yet.</summary>
    public static RiskGovernanceRecord Empty { get; } = new(0, null, [], [], [], new Dictionary<Uuid, Uuid>());

    /// <summary>The revision of the risk's governance stream.</summary>
    public long Revision { get; } = revision;

    /// <summary>The current owner, or null.</summary>
    public RiskOwnerView? Owner { get; } = owner;

    /// <summary>Every control treatment of every line of the risk, in the order proposed.</summary>
    public IReadOnlyList<RiskControlTreatmentView> Treatments { get; } = treatments;

    /// <summary>Every trigger in the order raised.</summary>
    public IReadOnlyList<RiskReassessmentTriggerView> Triggers { get; } = triggers;

    /// <summary>Every action of the risk in the order added.</summary>
    public IReadOnlyList<RiskTreatmentActionView> Actions { get; } = actions;

    /// <summary>The submitting member of every completion submission.</summary>
    public IReadOnlyDictionary<Uuid, Uuid> Submitters { get; } = submitters;
}
