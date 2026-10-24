using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Composes the governed application a reader sees. A target's own stream replays only its own facts; the
///     published candidate of a batch is shown once that batch's own publication fact exists, and the stream is
///     never changed by this read.
/// </summary>
static class ApplicationImportVisibility
{
    public static async ValueTask<DeclaredApplication> HydrateApplicationAsync(this IAggregateReader reader,
        Uuid tenantId, Uuid applicationId, CancellationToken ct = default)
    {
        var application = await reader.HydrateAsync(new DeclaredApplication(tenantId, applicationId), ct)
            .ConfigureAwait(false);
        foreach (var hold in application.ImportHolds.Where(hold => hold.IsActive && hold.IsApplied).ToArray())
        {
            var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenantId, hold.BatchId), ct)
                .ConfigureAwait(false);
            if (batch.Committed is not null)
                application.ShowPublished(hold.BatchId);
        }

        return application;
    }
}
