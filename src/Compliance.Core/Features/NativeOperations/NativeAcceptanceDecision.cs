namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>What the owner's accepted facts say about a candidate binding.</summary>
public enum NativeAcceptanceDecision
{
    /// <summary>No accepted fact carries the selected logical id; continue as a new operation.</summary>
    None,

    /// <summary>An accepted fact carries the same binding; return its original response without append.</summary>
    Replayed,

    /// <summary>An accepted fact carries the same logical id with a different binding.</summary>
    Conflict,
}
