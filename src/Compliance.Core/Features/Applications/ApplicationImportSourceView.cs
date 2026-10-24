using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     A source with the claims its published batches established. Claims derive only from publication facts of
///     batches the source settled as published, in settlement order: a later observation of a record replaces an
///     earlier one and a retirement removes the claim. Raw rows and plans confer no authority.
/// </summary>
sealed record ApplicationImportSourceView(ApplicationImportSource Source,
    IReadOnlyList<ApplicationImportSourceClaimRecord> Claims)
{
    public ApplicationImportSourceClaimRecord? Find(string sourceRecordId) =>
        Claims.FirstOrDefault(claim => string.Equals(claim.Observation.SourceRecordId, sourceRecordId,
            StringComparison.Ordinal));

    /// <summary>Loads the source and derives its claims from the publication facts of its published batches.</summary>
    public static async ValueTask<Result<ApplicationImportSourceView>> LoadAsync(IAggregateReader reader,
        IImmutableContentStore content, Uuid tenantId, string sourceKey, string sourceNamespace,
        CancellationToken ct, Uuid selectedLogicalId = default)
    {
        var source = await reader.HydrateAsync(new ApplicationImportSource(tenantId, sourceKey, sourceNamespace,
            selectedLogicalId), ct).ConfigureAwait(false);
        var claims = new Dictionary<string, ApplicationImportSourceClaimRecord>(StringComparer.Ordinal);
        foreach (var batchId in source.PublishedBatchIds)
        {
            var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenantId, batchId), ct)
                .ConfigureAwait(false);
            if (batch.Committed is not { } committed)
                return Result<ApplicationImportSourceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The published batch of this source is not visible yet.", isTransient: true));
            IReadOnlyList<ApplicationImportPlannedRow> rows;
            try
            {
                rows = await ApplicationImportContent.ReadAsync<IReadOnlyList<ApplicationImportPlannedRow>>(content,
                    tenantId, committed.FrozenRows, ct).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<ApplicationImportSourceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The published rows of this source cannot be verified: {exception.Message}", isTransient: true));
            }

            foreach (var row in rows)
            {
                if (row.Decision == "retire")
                    claims.Remove(row.SourceRecordId);
                else
                    claims[row.SourceRecordId] = new ApplicationImportSourceClaimRecord(batchId, row,
                        committed.CommittedAt);
            }
        }

        return Result<ApplicationImportSourceView>.Success(new ApplicationImportSourceView(source,
            Array.AsReadOnly(claims.Values.OrderBy(claim => claim.Observation.SourceRecordId, StringComparer.Ordinal)
                .ToArray())));
    }

    /// <summary>Claims of the source that a complete-source batch no longer observes.</summary>
    public IReadOnlyList<ApplicationImportSourceClaimRecord> Missing(ApplicationImportBatch batch,
        IReadOnlyList<ApplicationImportStagedRow> rows)
    {
        if (batch.Coverage != "declared_complete")
            return [];
        var present = rows.Select(row => row.SourceRecordId).ToHashSet(StringComparer.Ordinal);
        return Claims.Where(claim => !present.Contains(claim.Observation.SourceRecordId)).ToArray();
    }
}
