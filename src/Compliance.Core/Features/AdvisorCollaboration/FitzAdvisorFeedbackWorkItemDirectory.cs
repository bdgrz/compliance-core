using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

sealed class FitzAdvisorFeedbackWorkItemDirectory(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client, "kv://bdgrz/advisor-feedback-work-items/projection-v2",
        ProjectorName), IAdvisorFeedbackWorkItemProjection, IAccountableWorkItemDirectoryReader
{
    public const string ProjectorName = "AdvisorFeedbackWorkItemV2";
    Uuid? _batchTenantId;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => [WorkSource.AdvisorFeedback];

    public EventStreamPattern SourcePattern(Uuid tenantId) => EventStreamPattern.ForPattern(
        tenantId.ToString(), AdvisorFeedbackThread.Area);

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginTenantBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginTenantBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginTenantBatchAsync(ProjectionBatchContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != AdvisorFeedbackThread.Area ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) ||
            tenantId == Uuid.Empty)
            throw new InvalidOperationException(
                "An advisor feedback work batch requires its tenant's feedback thread area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var (tenantId, programId, handoffId, feedbackId) = Identity(domainEvent);
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || handoffId == Uuid.Empty || feedbackId == Uuid.Empty)
            throw InvalidEvent();
        EnsureTenant(tenantId);
        var metadata = domainEvent.Metadata;
        if (metadata.EventId == Uuid.Empty || metadata.AggregateId != AdvisorFeedbackThread.IdentityOf(tenantId,
                programId, handoffId, feedbackId) || metadata.AggregateVersion == 0)
            throw InvalidEvent();

        var current = await AdvisorFeedbackWorkItemSchema.Threads.GetAsync(Transaction,
            AdvisorFeedbackWorkItemSchema.ThreadKey(tenantId, programId, handoffId, feedbackId), ct).ConfigureAwait(false);
        if (current is null)
        {
            if (domainEvent is not AdvisorFeedbackRecorded || metadata.AggregateVersion != 1)
                throw InvalidEvent();
        }
        else
        {
            if (current.TenantId != tenantId || current.ProgramId != programId ||
                current.HandoffId != handoffId || current.FeedbackId != feedbackId)
                throw InvalidEvent();
            if (metadata.AggregateVersion == current.LastAggregateVersion &&
                metadata.EventId == current.LastEventId)
                return;
            if (metadata.AggregateVersion <= current.LastAggregateVersion ||
                metadata.AggregateVersion != current.LastAggregateVersion + 1)
                throw InvalidEvent();
        }

        switch (domainEvent)
        {
            case AdvisorFeedbackRecorded recorded:
                await RecordAsync(recorded, ct).ConfigureAwait(false);
                break;
            case AdvisorFeedbackCorrected corrected:
                await CorrectAsync(corrected, ct).ConfigureAwait(false);
                break;
            case AdvisorFeedbackResponded responded:
                await RespondAsync(responded, ct).ConfigureAwait(false);
                break;
            case AdvisorFeedbackResolved resolved:
                await ResolveAsync(resolved, ct).ConfigureAwait(false);
                break;
        }

        var next = new AdvisorFeedbackWorkItemProjectionState(tenantId, programId, handoffId, feedbackId,
            metadata.EventId, metadata.AggregateVersion);
        if (current is null)
            await AdvisorFeedbackWorkItemSchema.Threads.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await AdvisorFeedbackWorkItemSchema.Threads.ReplaceAsync(Transaction, current, next, ct)
                .ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await AdvisorFeedbackWorkItemSchema.Feedback.QueryAsync(tx,
                AdvisorFeedbackWorkItemSchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var entry in page.Items)
            {
                if (entry.TenantId != tenantId || entry.ProgramId != programId ||
                    entry.WorkItemId != AdvisorFeedbackWorkItemSchema.WorkItemId(entry.TenantId, entry.ProgramId, entry.HandoffId, entry.FeedbackId))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                if (entry.IsResolved || entry.Kind == "validated")
                    continue;
                candidates.Add(ToView(entry).ToCandidate());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask RecordAsync(AdvisorFeedbackRecorded ev, CancellationToken ct)
    {
        var contribution = await AdvisorFeedbackReader.ReadRecordedAsync(content, ev, ct).ConfigureAwait(false);
        var source = ev.SelectedSource ?? new AdvisorRecordVersion(ev.RecordId,
            ev.RecordVersionId);
        if (ev.FeedbackId == Uuid.Empty || ev.RecordId == Uuid.Empty ||
            source.RecordId != ev.RecordId || source.VersionId != ev.RecordVersionId ||
            !source.HasValidIdentity || ev.Kind is not ("comment" or "change_request" or "validated") ||
            string.IsNullOrWhiteSpace(contribution.Body) || contribution.Body.Trim() != contribution.Body || !ValidActor(ev.Author) ||
            ev.CreatedAt == default)
            throw InvalidEvent();
        var existing = await AdvisorFeedbackWorkItemSchema.Feedback.GetAsync(Transaction,
            AdvisorFeedbackWorkItemSchema.ThreadKey(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId), ct).ConfigureAwait(false);
        if (existing is not null)
            throw InvalidEvent();
        var entry = new AdvisorFeedbackWorkItemEntry(ev.TenantId, ev.ProgramId, ev.HandoffId,
            ev.FeedbackId, AdvisorFeedbackWorkItemSchema.WorkItemId(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId),
            ev.RecordId, ev.Kind, contribution.Body, ev.Author, ev.CreatedAt, false, false);
        await AdvisorFeedbackWorkItemSchema.Feedback.InsertAsync(Transaction, entry, ct)
            .ConfigureAwait(false);
    }

    async ValueTask CorrectAsync(AdvisorFeedbackCorrected ev, CancellationToken ct)
    {
        var authored = await AdvisorAuthored.ReadAsync(content, ev, ct).ConfigureAwait(false);
        if (ev.FeedbackId == Uuid.Empty || ev.CorrectionId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(authored.CorrectedBody) || authored.CorrectedBody.Trim() != authored.CorrectedBody ||
            string.IsNullOrWhiteSpace(authored.CorrectionReason) ||
            authored.CorrectionReason.Trim() != authored.CorrectionReason || !ValidActor(ev.CorrectedBy) ||
            ev.CorrectedAt == default)
            throw InvalidEvent();
        var current = await GetFeedbackAsync(ev.TenantId, ev.ProgramId, ev.HandoffId,
            ev.FeedbackId, ct).ConfigureAwait(false);
        await AdvisorFeedbackWorkItemSchema.Feedback.ReplaceAsync(Transaction, current,
            current with { Body = authored.CorrectedBody }, ct).ConfigureAwait(false);
    }

    async ValueTask RespondAsync(AdvisorFeedbackResponded ev, CancellationToken ct)
    {
        var authored = await AdvisorAuthored.ReadAsync(content, ev, ct).ConfigureAwait(false);
        if (ev.FeedbackId == Uuid.Empty || string.IsNullOrWhiteSpace(authored.Response) ||
            authored.Response.Trim() != authored.Response || !ValidActor(ev.RespondedBy) ||
            ev.RespondedAt == default)
            throw InvalidEvent();
        var current = await GetFeedbackAsync(ev.TenantId, ev.ProgramId, ev.HandoffId,
            ev.FeedbackId, ct).ConfigureAwait(false);
        await AdvisorFeedbackWorkItemSchema.Feedback.ReplaceAsync(Transaction, current,
            current with { HasResponded = true }, ct).ConfigureAwait(false);
    }

    async ValueTask ResolveAsync(AdvisorFeedbackResolved ev, CancellationToken ct)
    {
        if (ev.FeedbackId == Uuid.Empty || !ValidActor(ev.ResolvedBy) || ev.ResolvedAt == default)
            throw InvalidEvent();
        var current = await GetFeedbackAsync(ev.TenantId, ev.ProgramId, ev.HandoffId,
            ev.FeedbackId, ct).ConfigureAwait(false);
        if (current.IsResolved)
            throw InvalidEvent();
        await AdvisorFeedbackWorkItemSchema.Feedback.ReplaceAsync(Transaction, current,
            current with { IsResolved = true }, ct).ConfigureAwait(false);
    }

    async ValueTask<AdvisorFeedbackWorkItemEntry> GetFeedbackAsync(Uuid tenantId, Uuid programId,
        Uuid handoffId, Uuid feedbackId, CancellationToken ct)
    {
        var current = await AdvisorFeedbackWorkItemSchema.Feedback.GetAsync(Transaction,
            AdvisorFeedbackWorkItemSchema.ThreadKey(tenantId, programId, handoffId, feedbackId), ct).ConfigureAwait(false) ?? throw InvalidEvent();
        if (current.TenantId != tenantId || current.ProgramId != programId ||
            current.HandoffId != handoffId || current.FeedbackId != feedbackId ||
            current.WorkItemId != AdvisorFeedbackWorkItemSchema.WorkItemId(tenantId, programId, handoffId, feedbackId))
            throw InvalidEvent();
        return current;
    }

    static AccountableWorkItemView ToView(AdvisorFeedbackWorkItemEntry entry)
    {
        var prefix = $"/api/v1/tenants/{entry.TenantId}/programs/{entry.ProgramId}/" +
                     $"advisor-handoffs/{entry.HandoffId}/feedback/{entry.FeedbackId}";
        var nextAction = entry.HasResponded ? "resolve_feedback" : "respond_to_feedback";
        var actionPath = entry.HasResponded ? $"{prefix}/resolutions" : $"{prefix}/responses";
        var kind = entry.Kind.Replace('_', ' ');
        return new AccountableWorkItemView(entry.TenantId, entry.ProgramId, entry.WorkItemId,
            WorkSource.AdvisorFeedback, entry.FeedbackId, null, null,
            $"Advisor {kind} on record {entry.RecordId}", $"{entry.Author.Display}: {entry.Body}",
            null, null, nextAction, actionPath,
            new OperatingHolder(OperatingAuthority.ProgramManagerHolder, entry.ProgramId), null,
            [], entry.CreatedAt);
    }

    static (Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid FeedbackId) Identity(DomainEvent domainEvent) =>
        domainEvent switch
        {
            AdvisorFeedbackRecorded ev => (ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId),
            AdvisorFeedbackCorrected ev => (ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId),
            AdvisorFeedbackResponded ev => (ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId),
            AdvisorFeedbackResolved ev => (ev.TenantId, ev.ProgramId, ev.HandoffId, ev.FeedbackId),
            _ => throw InvalidEvent(),
        };

    void EnsureTenant(Uuid tenantId)
    {
        if (_batchTenantId != tenantId)
            throw new InvalidOperationException(
                "An advisor feedback event must belong to its tenant projection batch.");
    }

    static bool ValidActor(ActorReference? actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Kind) && !string.IsNullOrWhiteSpace(actor.Id) &&
        !string.IsNullOrWhiteSpace(actor.Display);

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The advisor feedback work projection has an invalid tenant or program scope.");

    static InvalidOperationException InvalidEvent() => new(
        "The advisor feedback work event is invalid or out of order.");
}
