namespace Bdgrz.Compliance.Hosting.Persistence;

/// <summary>A complete attributed batch was rejected before its append session opened.</summary>
sealed class EventFrameSizeException(long operationBytes)
    : Exception("An attributed event exceeds the 65,535-byte storage operation limit.")
{
    public long OperationBytes { get; } = operationBytes;
}
