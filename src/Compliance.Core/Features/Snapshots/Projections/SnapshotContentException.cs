namespace Bdgrz.Compliance.Features.Snapshots.Projections;

sealed class SnapshotContentException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
