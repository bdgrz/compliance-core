namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>Reported state of an asynchronous operation.</summary>
public enum OperationStatus
{
    /// <summary>Accepted and still progressing.</summary>
    Pending,

    /// <summary>Finished with a result.</summary>
    Completed,

    /// <summary>Finished with a business conflict.</summary>
    Conflict,

    /// <summary>Finished with a failure.</summary>
    Failed,

    /// <summary>Technical external effects await reconciliation.</summary>
    Unknown,
}
