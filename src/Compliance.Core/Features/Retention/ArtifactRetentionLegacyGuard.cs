using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Fails closed when a source still has combined-owner history for which no migration is selected.</summary>
static class ArtifactRetentionLegacyGuard
{
    public static async ValueTask<Result> RequireNoLegacyAsync(IDomainEventReader events, ArtifactRetentionSource source,
        CancellationToken ct)
    {
        var id = Uuid.CreateVersion5(source.TenantId, $"artifact_retention:{source.SourceKind}:{source.SourceId}");
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(source.TenantId.ToString(),
                           "artifact_retention"), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Stream.Resource == id.ToString() &&
                record.Event is ArtifactRetentionBound or ArtifactRetentionBasisRecorded or ArtifactLegalHoldPlaced or ArtifactLegalHoldReleased)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "This source has combined retention history; its verified owner migration is required before retention decisions or reads.", true));
        return Result.Success;
    }
}
