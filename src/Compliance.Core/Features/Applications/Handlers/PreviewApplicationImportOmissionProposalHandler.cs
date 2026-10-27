using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

public sealed class PreviewApplicationImportOmissionProposalHandler(IAggregateReader reader,
    IImmutableContentStore content, ApplicationPublicationReader publication, IDomainEventReader events)
    : IRequestHandler<PreviewApplicationImportOmissionProposal, ApplicationImportOmissionPreview>
{
    public async ValueTask<Result<ApplicationImportOmissionPreview>> HandleAsync(
        IRequestContext<PreviewApplicationImportOmissionProposal> context, CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await ApplicationImportOmissionSources.LoadAsync(reader, content, events, request.TenantId,
            request.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ApplicationImportOmissionPreview>.Failure(loaded.Error);
        var (batch, source, stagedRows) = loaded.Value;
        var history = await ApplicationImportBatchHistory.ReadAsync(events, batch, ct).ConfigureAwait(false);
        var missing = source.Missing(batch, stagedRows);
        var rows = missing.Select(claim => ApplicationImportOmissions.RetirementRow(source.Source.Id, claim)).ToArray();
        var blockers = ApplicationImportOmissionSources.Blockers.ToList();
        if (ApplicationImportOmissions.HasSharedOrPresentAlias(history, source, missing, stagedRows))
            blockers.Add("source_alias_cannot_propose_retirement");
        if (batch.Coverage != "declared_complete")
            blockers.Add("partial_source_cannot_propose_retirement");
        if (stagedRows.Any(row => row.ValidationFindings.Count > 0))
            blockers.Add("invalid_source_rows");
        if (batch.State != "preview_ready")
            blockers.Add("batch_not_preview_ready");
        var cut = await publication.BeginAsync(ct).ConfigureAwait(false);
        foreach (var row in rows)
        {
            var target = await cut.ReadAsync(request.TenantId, row.ApplicationId, ct).ConfigureAwait(false);
            if (!target.IsCreated || target.IsRetired || target.Revision != row.ExpectedApplicationRevision ||
                target.CheckPendingImportChanges() is not null)
                blockers.Add("source_claim_target_changed");
        }
        if (!await ApplicationImportOmissionSources.UnchangedAsync(reader, content, events, request.TenantId, batch, source, ct)
                .ConfigureAwait(false))
            return Result<ApplicationImportOmissionPreview>.Failure(ApplicationImportOmissionSources.Changed());
        return Result<ApplicationImportOmissionPreview>.Success(new(request.TenantId, batch.Id, batch.SourceKey!,
            batch.SourceNamespace!, batch.Coverage!, batch.Revision, source.Source.CommittedStreamPosition,
            batch.ContentDigest!, Array.AsReadOnly(rows), Array.AsReadOnly(blockers.Distinct(StringComparer.Ordinal).ToArray())));
    }
}
