using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Binds batch preparation to its complete captured native payload/identity and physical cut.</summary>
sealed record ApplicationImportBatchPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static ApplicationImportBatchPreparedCut Capture(ApplicationImportBatch batch) =>
        new(batch.Stream, batch.Version, batch.CommittedStreamPosition, batch.NativeCut.Digest);
    public bool Matches(ApplicationImportBatch batch) => this == Capture(batch);
    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The import batch changed while its exact source-derived content was prepared.", true));
}
