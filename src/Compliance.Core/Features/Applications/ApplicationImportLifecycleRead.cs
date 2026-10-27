using Bdgrz.Compliance.Features.Applications.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationImportLifecycleRead
{
    public static async ValueTask<Result<ApplicationImportBatch>> LoadAsync(IAggregateReader reader, Uuid tenantId,
        Uuid batchId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ApplicationImportBatch>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum import revision must be positive."));
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenantId, batchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.Id != batchId || batch.Stream.Realm != tenantId.ToString())
            return Result<ApplicationImportBatch>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The import batch was not found."));
        return minimumRevision is { } minimum && batch.Revision < minimum
            ? Result<ApplicationImportBatch>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The durable import batch has not reached the requested revision.", isTransient: true))
            : Result<ApplicationImportBatch>.Success(batch);
    }

    public static async ValueTask<Result> CheckAsync(IAggregateReader reader, ApplicationImportBatch batch,
        CancellationToken ct)
    {
        var tenantId = Uuid.Parse(batch.Stream.Realm, null);
        var current = await reader.HydrateAsync(new ApplicationImportBatch(tenantId, batch.Id), ct).ConfigureAwait(false);
        return current.CommittedStreamPosition == batch.CommittedStreamPosition &&
               current.Revision == batch.Revision && current.State == batch.State
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The import batch changed while reading the report; retry.", isTransient: true));
    }
}
