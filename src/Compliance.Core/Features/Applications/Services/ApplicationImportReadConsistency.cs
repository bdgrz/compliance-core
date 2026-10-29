using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Applications.Services;

public sealed class ApplicationImportReadConsistency(IApplicationImportDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result<ApplicationImportView>> GetFreshAsync(Uuid tenantId,
        Uuid batchId, long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The minimum import revision must be positive."));
        var source = await reader.HydrateAsync(new ApplicationImportBatch(tenantId, batchId), ct)
            .ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The import batch was not found."));
        var revision = source.Revision;
        if (minimumRevision is { } minimum && revision < minimum)
            return Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.Conflict, $"The import batch has not reached revision {minimum}.",
                isTransient: true));
        var view = await directory.GetAsync(tenantId, batchId, ct).ConfigureAwait(false);
        return view is null || view.TenantId != tenantId || view.BatchId != batchId ||
               view.Revision != revision || view.State != source.State ||
               (minimumRevision is { } wanted && view.Revision < wanted)
            ? Result<ApplicationImportView>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The import projection has not reached the batch revision.",
                isTransient: true))
            : Result<ApplicationImportView>.Success(view);
    }
}
