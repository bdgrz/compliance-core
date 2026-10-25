using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     A source with the claims its published batches established. Claims derive only from publication facts of
///     batches the source settled as published, in settlement order: a later observation of a record replaces an
///     earlier one and a retirement removes the claim. Raw rows and plans confer no authority.
/// </summary>
sealed record ApplicationImportSourceView(ApplicationImportSource Source,
    ApplicationImportSourceHistorySnapshot History, IReadOnlyList<ApplicationImportSourceClaimRecord> Claims)
{
    public ApplicationImportSourceClaimRecord? Find(string sourceRecordId) =>
        Claims.FirstOrDefault(claim => string.Equals(claim.Observation.SourceRecordId, sourceRecordId,
            StringComparison.Ordinal));

    /// <summary>Loads the source and derives its claims from the publication facts of its published batches.</summary>
    public static async ValueTask<Result<ApplicationImportSourceView>> LoadAsync(IAggregateReader reader,
        IImmutableContentStore content, IDomainEventReader events, Uuid tenantId, string sourceKey, string sourceNamespace,
        Uuid selectedLogicalId = default, CancellationToken ct = default)
    {
        try
        {
            var source = await reader.HydrateAsync(new ApplicationImportSource(tenantId, sourceKey, sourceNamespace,
                selectedLogicalId), ct).ConfigureAwait(false);
            var history = await ApplicationImportSourceHistory.ReadAsync(events, source, ct).ConfigureAwait(false);
            var claims = new Dictionary<string, ApplicationImportSourceClaimRecord>(StringComparer.Ordinal);
            foreach (var settled in history.Published)
            {
                var batchId = settled.BatchId;
                var admission = history.Find(batchId)?.Admitted
                    ?? throw new ImmutableContentIntegrityException("A source publication has no exact prior admission.");
                var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenantId, batchId), ct)
                    .ConfigureAwait(false);
                if (batch.Committed is not { } committed || batch.Plan is not { } sealedPlan)
                    throw new ImmutableContentUnavailableException("The published batch of this exact source is not visible yet.");
                var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, tenantId,
                    sealedPlan.FrozenPlan, ct).ConfigureAwait(false);
                ApplicationImportIntentBindings.Require(plan.Header, sealedPlan.Operation);
                if (batch.SourceKey != sourceKey || batch.SourceNamespace != sourceNamespace ||
                    committed.TenantId != tenantId || committed.BatchId != batchId || committed.SourceKey != sourceKey ||
                    committed.SourceNamespace != sourceNamespace || plan.BatchId != batchId || plan.SourceKey != sourceKey ||
                    plan.SourceNamespace != sourceNamespace || admission.Intent != sealedPlan.FrozenPlan ||
                    settled.Intent != sealedPlan.FrozenPlan || committed.Intent != sealedPlan.FrozenPlan ||
                    committed.FrozenRows != plan.Rows.Content || committed.SourceGeneration != plan.SourceRevision ||
                    admission.Revision != plan.SourceRevision + 1 ||
                    admission.Operation.CausationEventId != sealedPlan.Metadata.EventId ||
                    settled.Operation.CausationEventId != committed.Metadata.EventId ||
                    admission.Operation.RootOperationId != plan.Header.RootOperationId ||
                    settled.Operation.RootOperationId != plan.Header.RootOperationId ||
                    plan.Header.FirstOwner != batch.Stream || committed.Operation.RootOperationId != plan.Header.RootOperationId ||
                    admission.Operation.Actor != plan.Header.OriginalActor || settled.Operation.Actor != plan.Header.OriginalActor ||
                    admission.Operation.AuthoredIntentSha256 != plan.Header.AuthoredIntentSha256 ||
                    settled.Operation.AuthoredIntentSha256 != plan.Header.AuthoredIntentSha256 ||
                    committed.PlanSha256 != sealedPlan.PlanSha256)
                    throw new ImmutableContentIntegrityException("A published source claim differs from its exact native admission, immutable plan and terminal batch fact.");
                var rows = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(content,
                    tenantId, plan.Rows, ct).ConfigureAwait(false);
                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.SourceRecordId) || row.ApplicationId == Uuid.Empty)
                        throw new ImmutableContentIntegrityException("A published source observation has no exact record or application identity.");
                    if (row.Decision == "retire")
                        claims.Remove(row.SourceRecordId);
                    else
                        claims[row.SourceRecordId] = new ApplicationImportSourceClaimRecord(batchId, row, committed.CommittedAt);
                }
            }
            return Result<ApplicationImportSourceView>.Success(new ApplicationImportSourceView(source, history,
                Array.AsReadOnly(claims.Values.OrderBy(static claim => claim.Observation.SourceRecordId,
                    StringComparer.Ordinal).ToArray())));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<ApplicationImportSourceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact published import source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
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
