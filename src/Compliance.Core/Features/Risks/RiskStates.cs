namespace Bdgrz.Compliance.Features.Risks;

/// <summary>The outcome, trigger and action status vocabulary shared by the risk governance owners and their readers.</summary>
public static class RiskStates
{
    /// <summary>The outcome that accepts a treatment assertion or an action completion.</summary>
    public const string Accept = "accept";

    /// <summary>The outcome that rejects a treatment assertion or an action completion.</summary>
    public const string Reject = "reject";

    /// <summary>The trigger kind raised when a later risk method version is published.</summary>
    public const string MethodChanged = "method_changed";

    /// <summary>The trigger kind raised when a successor boundary version is approved.</summary>
    public const string BoundaryChanged = "boundary_changed";

    /// <summary>The status of a treatment action with open work.</summary>
    public const string ActionOpen = "open";

    /// <summary>The status of a treatment action whose completion awaits independent review.</summary>
    public const string ActionSubmitted = "completion_submitted";

    /// <summary>The status of a treatment action whose completion was independently accepted.</summary>
    public const string ActionCompleted = "completed";

    /// <summary>The status of a canceled treatment action.</summary>
    public const string ActionCancelled = "cancelled";

    /// <summary>The status of a proposed control treatment assertion.</summary>
    public const string TreatmentProposed = "proposed";

    /// <summary>The status of an accepted control treatment assertion.</summary>
    public const string TreatmentAccepted = "accepted";

    /// <summary>The status of a rejected control treatment assertion.</summary>
    public const string TreatmentRejected = "rejected";

    /// <summary>The status of an accepted assertion replaced by a newer accepted version of the same control.</summary>
    public const string TreatmentSuperseded = "superseded";

    /// <summary>The status of a retired control treatment assertion.</summary>
    public const string TreatmentRetired = "retired";
}
