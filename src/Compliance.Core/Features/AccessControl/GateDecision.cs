namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Whether a restoration receiver may apply its source change now.</summary>
public enum GateDecision
{
    /// <summary>No earlier incompatible withdrawal is unfinished and no later one superseded the restoration.</summary>
    Proceed,

    /// <summary>An earlier withdrawal of the same episode and reason is unfinished.</summary>
    Wait,

    /// <summary>A later withdrawal of the same episode and reason makes the restoration moot.</summary>
    Superseded,

    /// <summary>The guard holds no pending operation with that identity.</summary>
    Unknown,
}
