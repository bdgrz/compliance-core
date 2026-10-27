using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Verifies the original complete staged observation and its current correlations for read/preparation composition.</summary>
static class ApplicationImportBatchRows
{
    public static async Task<IReadOnlyList<ApplicationImportStagedRow>> ReadAsync(IImmutableContentStore content,
        ApplicationImportBatch batch, ApplicationImportBatchHistorySnapshot history, CancellationToken ct)
    {
        if (!history.Matches(batch) || batch.Staged is not { } staged)
            throw new ImmutableContentIntegrityException("The staged observation requires this exact certified batch prefix.");
        var rows = (await ApplicationImportContent.ReadAsync<ApplicationImportRowsManifest>(content, batch.TenantId,
            staged.RowsManifest, ct).ConfigureAwait(false)).Rows;
        if (rows is null || rows.Count != staged.RowCount || rows.Count(row => row.ValidationFindings.Count > 0) != staged.InvalidCount ||
            rows.Where((row, index) => row.RowNumber != index + 1 ||
                row.RowId != ApplicationImportEffects.RowId(batch.Id, index + 1)).Any())
            throw new ImmutableContentIntegrityException("The complete staged row identities/counts differ from native admission.");
        var byId = rows.ToDictionary(static row => row.RowId);
        if (history.Correlations.Any(choice => !byId.TryGetValue(choice.RowId, out var row) ||
            row.SourceRecordId != choice.SourceRecordId || row.ValidationFindings.Count != 0))
            throw new ImmutableContentIntegrityException("A correlation is outside its exact valid staged source row.");
        var observation = new StageApplicationImport(batch.TenantId, staged.SubmissionId, staged.SourceKey,
            staged.SourceNamespace, staged.Coverage, rows.Select(static row => new ApplicationImportInputRow(
                row.SourceRecordId, row.Name, row.Purpose, row.OwnerReference)).ToArray());
        if (ApplicationImportBatch.ObservationSha256(observation) != staged.ContentSha256)
            throw new ImmutableContentIntegrityException("The exact staged row bytes do not match their original logical observation digest.");
        return rows;
    }
}
