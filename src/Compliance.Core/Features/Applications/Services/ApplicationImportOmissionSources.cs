using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.History;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Applications.Services;

static class ApplicationImportOmissionSources
{
    internal static readonly IReadOnlyList<string> Blockers = Array.AsReadOnly(new[]
        { "missing_source_retirement_unavailable", "retirement_impact_unavailable" });

    internal static async ValueTask<Result<(ApplicationImportBatch Batch, ApplicationImportSourceView Source,
        IReadOnlyList<ApplicationImportStagedRow> Rows)>> LoadAsync(IAggregateReader reader,
        IImmutableContentStore content, IDomainEventReader events, Uuid tenant, Uuid batchId, CancellationToken ct)
    {
        var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenant, batchId), ct).ConfigureAwait(false);
        if (!batch.IsCreated || batch.SourceKey is not { } key || batch.SourceNamespace is not { } space)
            return Result<(ApplicationImportBatch, ApplicationImportSourceView, IReadOnlyList<ApplicationImportStagedRow>)>
                .Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        var source = await ApplicationImportSourceView.LoadAsync(reader, content, events, tenant, key, space, ct: ct)
            .ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<(ApplicationImportBatch, ApplicationImportSourceView, IReadOnlyList<ApplicationImportStagedRow>)>
                .Failure(source.Error!);
        try
        {
            var history = await ApplicationImportBatchHistory.ReadAsync(events, batch, ct).ConfigureAwait(false);
            var rows = await ApplicationImportBatchRows.ReadAsync(content, batch, history, ct).ConfigureAwait(false);
            return Result<(ApplicationImportBatch, ApplicationImportSourceView, IReadOnlyList<ApplicationImportStagedRow>)>
                .Success((batch, source.Value!, rows));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<(ApplicationImportBatch, ApplicationImportSourceView, IReadOnlyList<ApplicationImportStagedRow>)>
                .Failure(new RequestError(RequestErrorKind.Conflict, "The staged rows cannot be verified yet.",
                    isTransient: error is ImmutableContentUnavailableException));
        }
    }

    internal static async ValueTask<bool> UnchangedAsync(IAggregateReader reader, IImmutableContentStore content,
        IDomainEventReader events, Uuid tenant, ApplicationImportBatch batch, ApplicationImportSourceView source, CancellationToken ct)
    {
        var latest = await LoadAsync(reader, content, events, tenant, batch.Id, ct).ConfigureAwait(false);
        return latest.IsSuccess && latest.Value.Batch.CommittedStreamPosition == batch.CommittedStreamPosition &&
            latest.Value.Batch.ContentDigest == batch.ContentDigest && latest.Value.Batch.SourceKey == batch.SourceKey &&
            latest.Value.Batch.SourceNamespace == batch.SourceNamespace &&
            latest.Value.Source.Source.CommittedStreamPosition == source.Source.CommittedStreamPosition &&
            latest.Value.Batch.Revision == batch.Revision && latest.Value.Batch.State == batch.State;
    }

    internal static RequestError Changed() => new(RequestErrorKind.Conflict,
        "The import source changed while reading omission proposal facts.", isTransient: true);
}
