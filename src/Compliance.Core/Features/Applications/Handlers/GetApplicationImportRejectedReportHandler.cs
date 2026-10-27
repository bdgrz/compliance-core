using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

public sealed class GetApplicationImportRejectedReportHandler(IAggregateReader reader, IImmutableContentStore content,
    ApplicationPublicationReader publication, IDomainEventReader events)
    : IRequestHandler<GetApplicationImportRejectedReport, ApplicationImportRejectedReport>
{
    public async ValueTask<Result<ApplicationImportRejectedReport>> HandleAsync(IRequestContext<GetApplicationImportRejectedReport> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await ApplicationImportLifecycleRead.LoadAsync(reader, request.TenantId,
            request.BatchId, request.MinimumRevision, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ApplicationImportRejectedReport>.Failure(loaded.Error);
        var batch = loaded.Value;
        var history = await ApplicationImportBatchHistory.ReadAsync(events, batch, ct).ConfigureAwait(false);
        var state = batch.State;
        var rejected = new List<ApplicationImportRejectedRow>();
        var cut = await publication.BeginAsync(ct).ConfigureAwait(false);
        var batchFindings = new List<string>();
        var omissions = await ApplicationImportOmissionSources.LoadAsync(reader, content, events, request.TenantId,
            request.BatchId, ct).ConfigureAwait(false);
        if (!omissions.IsSuccess)
            return Result<ApplicationImportRejectedReport>.Failure(omissions.Error);
        var (_, source, rows) = omissions.Value;
        var missing = source.Missing(batch, rows);
        if (state == "preview_ready" && missing.Count > 0)
        {
            batchFindings.Add("missing_source_retirement_unavailable");
            batchFindings.Add("retirement_impact_unavailable");
        }

        if (state == "preview_ready" && source.Source.ActiveBatchId is { } active && active != batch.Id)
            batchFindings.Add("source_acceptance_in_progress");
        foreach (var row in rows)
        {
            var findings = row.ValidationFindings.ToList();
            if (state == "failed")
                findings.Add("batch_execution_failed");
            else if (state is "canceled" or "canceling")
                findings.Add("batch_canceled");
            else if (state == "preview_ready" && findings.Count == 0)
            {
                var choice = history.GetCorrelation(row.RowId);
                var claim = source.Find(row.SourceRecordId!);
                if (choice is null && claim is null)
                    findings.Add("correlation_required");
                else if (claim is not null && choice is not null &&
                         (choice.ApplicationId != claim.Observation.ApplicationId || choice.Decision == "create_new"))
                    findings.Add("source_claim_conflict");
                else if (choice?.Decision != "create_new")
                {
                    var targetId = choice?.ApplicationId ?? claim!.Observation.ApplicationId;
                    var expectedRevision = choice?.ExpectedApplicationRevision ?? claim?.Observation.ExpectedApplicationRevision ?? 1;
                    var target = await cut.ReadAsync(request.TenantId, targetId, ct).ConfigureAwait(false);
                    if (!target.IsCreated || target.Id != targetId || target.Stream.Realm != request.TenantId.ToString())
                        findings.Add("target_unavailable");
                    else if (target.IsRetired)
                        findings.Add("target_retired");
                    else if (target.Revision != expectedRevision)
                        findings.Add("target_revision_conflict");
                }
            }
            if (findings.Count > 0)
                rejected.Add(new(row.RowId, row.RowNumber, row.SourceRecordId, findings.AsReadOnly()));
        }
        var fence = await ApplicationImportLifecycleRead.CheckAsync(reader, batch, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ApplicationImportRejectedReport>.Failure(fence.Error);
        var failure = batch.Failed;
        return Result<ApplicationImportRejectedReport>.Success(new(request.TenantId, batch.Id,
            batch.Revision, state, rejected.AsReadOnly(), batchFindings.AsReadOnly(), failure?.FailureCode, failure?.RowId));
    }
}
