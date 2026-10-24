namespace Bdgrz.Compliance.Features.Processes;

/// <summary>Lifecycle of a process record. Only Pending and Aborting records are resumed.</summary>
public enum ProcessState
{
    /// <summary>Stages remain to dispatch.</summary>
    Pending,

    /// <summary>An abort was requested; abort receivers fence first, then settle/release stages run.</summary>
    Aborting,

    /// <summary>Every stage completed, or a receiver proved supersession.</summary>
    Completed,

    /// <summary>Abort receivers and settlement stages completed.</summary>
    Aborted,

    /// <summary>A receiver returned conflicting intent or an invalid proof; repair is manual.</summary>
    Quarantined,
}
