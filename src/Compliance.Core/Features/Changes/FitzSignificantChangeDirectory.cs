using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

sealed class FitzSignificantChangeDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/significant-change-directory/projection-v1",
        SignificantChangeDirectoryProjector.ProjectorName),
      ISignificantChangeDirectoryReader, ISignificantChangeDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        (Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
            DateTimeOffset OccurredAt, SignificantChangeView? State) envelope = domainEvent switch
            {
                SignificantChangeRecorded ev => (ev.TenantId, ev.SubjectId, ev.Revision,
                    ev.ActorMemberId, ev.OccurredAt, ev.State),
                SignificantChangeRevised ev => (ev.TenantId, ev.SubjectId, ev.Revision,
                    ev.ActorMemberId, ev.OccurredAt, ev.State),
                SignificantChangeImpactAssessed ev => (ev.TenantId, ev.SubjectId, ev.Revision,
                    ev.ActorMemberId, ev.OccurredAt, ev.State),
                ImpactReviewWorkLinked ev => (ev.TenantId, ev.SubjectId, ev.Revision,
                    ev.ActorMemberId, ev.OccurredAt, ev.State),
                SignificantChangeSystemDescriptionDecisionRecorded ev => (ev.TenantId,
                    ev.SubjectId, ev.Revision, ev.ActorMemberId, ev.OccurredAt, ev.State),
                _ => (Uuid.Empty, Uuid.Empty, 0L, (Uuid?)null, default(DateTimeOffset), null),
            };
        var (tenantId, subjectId, revision, actorMemberId, occurredAt, state) = envelope;
        if (state is null)
            return;
        var actorMismatch = actorMemberId is { } actor && actor != state.ActorMemberId;
        if (tenantId == Uuid.Empty || subjectId == Uuid.Empty || revision <= 0 ||
            occurredAt == default || state.TenantId != tenantId || state.ProgramId == Uuid.Empty ||
            state.SignificantChangeId != subjectId || state.Revision != revision ||
            state.UpdatedAt != occurredAt || actorMismatch || state.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(state.ActorDisplay) || state.Current is null ||
            state.Versions is null || state.Versions.Count is < 1 or > 32 ||
            state.CurrentVersionNumber is < 1 or > 32 ||
            state.Versions.Count != state.CurrentVersionNumber ||
            state.Versions[^1].Version != state.CurrentVersionNumber ||
            state.Versions[^1].ContentSha256 != state.Current.ContentSha256 ||
            state.Assessments is null || state.Assessments.Count > state.Versions.Count ||
            state.ReviewWorkLinks is null || state.ReviewWorkLinks.Count > 1000 ||
            state.SystemDescriptionDecisions is null ||
            state.SystemDescriptionDecisions.Count > state.Assessments.Count)
            throw new InvalidOperationException("The significant-change projection event is invalid.");

        var summary = ToSummary(state);
        var current = await SignificantChangeDirectorySchema.Details.GetAsync(Transaction,
            subjectId, ct).ConfigureAwait(false);
        if (current is null)
        {
            if (domainEvent is not SignificantChangeRecorded || revision != 1 ||
                state.Versions.Count != 1 || state.CurrentVersionNumber != 1)
                throw new InvalidOperationException(
                    "A significant change must be recorded before later revisions are projected.");
            await SignificantChangeDirectorySchema.Details.InsertAsync(Transaction, state, ct)
                .ConfigureAwait(false);
            await SignificantChangeDirectorySchema.Summaries.InsertAsync(Transaction, summary, ct)
                .ConfigureAwait(false);
            return;
        }
        if (current.TenantId != tenantId || current.ProgramId != state.ProgramId ||
            current.SignificantChangeId != subjectId)
            throw new InvalidOperationException(
                "A significant-change identity changed during projection.");
        if (revision < current.Revision)
            return;
        if (revision == current.Revision)
        {
            if (!SameSnapshot(current, state))
                throw new InvalidOperationException(
                    "A significant-change revision has conflicting projection content.");
            var existingSummary = await SignificantChangeDirectorySchema.Summaries.GetAsync(
                Transaction, subjectId, ct).ConfigureAwait(false);
            if (existingSummary != summary)
                throw new InvalidOperationException(
                    "A significant-change summary has conflicting projection content.");
            return;
        }
        if (revision != current.Revision + 1)
            throw new InvalidOperationException("The significant-change projection skipped a revision.");

        var currentSummary = await SignificantChangeDirectorySchema.Summaries.GetAsync(Transaction,
            subjectId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A significant-change summary is missing from its projection.");
        await SignificantChangeDirectorySchema.Details.ReplaceAsync(Transaction, current, state, ct)
            .ConfigureAwait(false);
        await SignificantChangeDirectorySchema.Summaries.ReplaceAsync(Transaction, currentSummary,
            summary, ct).ConfigureAwait(false);
    }

    public async ValueTask<SignificantChangeView?> GetAsync(Uuid tenantId, Uuid changeId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await SignificantChangeDirectorySchema.Details.GetAsync(tx, changeId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<SignificantChangeSummaryView>> ListAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await SignificantChangeDirectorySchema.Summaries.QueryAsync(tx,
            SignificantChangeDirectorySchema.ByProgramUpdatedAt.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }

    static SignificantChangeSummaryView ToSummary(SignificantChangeView state)
    {
        var current = state.Current ?? throw new InvalidOperationException(
            "A significant-change projection requires current content.");
        return new SignificantChangeSummaryView(state.TenantId, state.ProgramId,
            state.SignificantChangeId, current.Content.Title, state.Status, state.UpdatedAt);
    }

    static bool SameSnapshot(SignificantChangeView first, SignificantChangeView second)
    {
        var firstJson = JsonSerializer.SerializeToUtf8Bytes(first,
            ComplianceCoreJsonContext.Default.SignificantChangeView);
        var secondJson = JsonSerializer.SerializeToUtf8Bytes(second,
            ComplianceCoreJsonContext.Default.SignificantChangeView);
        return firstJson.AsSpan().SequenceEqual(secondJson);
    }
}
