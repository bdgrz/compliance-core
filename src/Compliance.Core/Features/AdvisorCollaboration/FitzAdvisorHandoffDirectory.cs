using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

sealed class FitzAdvisorHandoffDirectory : FitzKvProjectionStore,
    IAdvisorHandoffDirectoryReader, IAdvisorHandoffDirectoryProjection
{
    public const string ProjectorName = "AdvisorHandoffDirectoryV1";
    readonly IKvClient _directoryClient;
    Uuid? _batchTenantId;

    public FitzAdvisorHandoffDirectory(IKvClient client)
        : base(client, "kv://bdgrz/advisor-handoff-directory/projection-v1", ProjectorName) =>
        _directoryClient = client;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginTenantBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginTenantBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginTenantBatchAsync(ProjectionBatchContext context,
        CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != "advisor-handoffs" ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) ||
            tenantId == Uuid.Empty)
            throw new InvalidOperationException(
                "An advisor handoff directory batch requires its tenant's handoff area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString(), "advisor-handoffs")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        EnsureTenant(domainEvent switch
        {
            AdvisorHandoffCreated ev => ev.TenantId,
            AdvisorHandoffSelectionRevised ev => ev.TenantId,
            AdvisorHandoffReadiedForReview ev => ev.TenantId,
            AdvisorHandoffClosed ev => ev.TenantId,
            AdvisorHandoffRevoked ev => ev.TenantId,
            AdvisorFeedbackRecorded ev => ev.TenantId,
            AdvisorFeedbackCorrected ev => ev.TenantId,
            AdvisorFeedbackResponded ev => ev.TenantId,
            AdvisorFeedbackResolved ev => ev.TenantId,
            _ => throw InvalidEvent(),
        });
        switch (domainEvent)
        {
            case AdvisorHandoffCreated created:
                await ApplyCreatedAsync(created, ct).ConfigureAwait(false);
                break;
            case AdvisorHandoffSelectionRevised revised:
                if (!ValidIdentity(revised.TenantId, revised.ProgramId, revised.HandoffId,
                        revised.RevisedAt) ||
                    !ValidSelection(revised.RecordVersions) ||
                    string.IsNullOrWhiteSpace(revised.ChangeReason) ||
                    revised.ChangeReason.Trim() != revised.ChangeReason || !ValidActor(revised.RevisedBy))
                    throw InvalidEvent();
                await ApplyLifecycleAsync(revised, revised.TenantId, revised.ProgramId, revised.HandoffId,
                    revised.Revision, revised.RevisedAt, "draft", ["draft", "ready_for_review"],
                    ct, revised.RecordVersions)
                    .ConfigureAwait(false);
                break;
            case AdvisorHandoffReadiedForReview readied:
                if (!ValidIdentity(readied.TenantId, readied.ProgramId, readied.HandoffId,
                        readied.ReadiedAt) || !ValidActor(readied.ReadiedBy))
                    throw InvalidEvent();
                await ApplyLifecycleAsync(readied, readied.TenantId, readied.ProgramId, readied.HandoffId,
                    readied.Revision, readied.ReadiedAt, "ready_for_review", ["draft"], ct)
                    .ConfigureAwait(false);
                break;
            case AdvisorHandoffClosed closed:
                if (!ValidIdentity(closed.TenantId, closed.ProgramId, closed.HandoffId,
                        closed.ClosedAt) || string.IsNullOrWhiteSpace(closed.Rationale) ||
                    closed.Rationale.Trim() != closed.Rationale || !ValidActor(closed.ClosedBy))
                    throw InvalidEvent();
                await ApplyLifecycleAsync(closed, closed.TenantId, closed.ProgramId, closed.HandoffId,
                    closed.Revision, closed.ClosedAt, "closed", ["draft", "ready_for_review"], ct)
                    .ConfigureAwait(false);
                break;
            case AdvisorHandoffRevoked revoked:
                if (!ValidIdentity(revoked.TenantId, revoked.ProgramId, revoked.HandoffId,
                        revoked.RevokedAt) || string.IsNullOrWhiteSpace(revoked.Rationale) ||
                    revoked.Rationale.Trim() != revoked.Rationale || !ValidActor(revoked.RevokedBy))
                    throw InvalidEvent();
                await ApplyLifecycleAsync(revoked, revoked.TenantId, revoked.ProgramId, revoked.HandoffId,
                    revoked.Revision, revoked.RevokedAt, "revoked", ["draft", "ready_for_review"], ct)
                    .ConfigureAwait(false);
                break;
            case AdvisorFeedbackRecorded feedback:
                await ApplyFeedbackRecordedAsync(feedback, ct).ConfigureAwait(false);
                break;
            case AdvisorFeedbackCorrected correction:
                await ApplyFeedbackCorrectedAsync(correction, ct).ConfigureAwait(false);
                break;
            case AdvisorFeedbackResponded response:
                await ApplyFeedbackRespondedAsync(response, ct).ConfigureAwait(false);
                break;
            case AdvisorFeedbackResolved resolution:
                await ApplyFeedbackResolvedAsync(resolution, ct).ConfigureAwait(false);
                break;
        }
    }

    void EnsureTenant(Uuid tenantId)
    {
        if (_batchTenantId != tenantId)
            throw new InvalidOperationException(
                "An advisor handoff event must belong to its tenant projection batch.");
    }

    async ValueTask ApplyCreatedAsync(AdvisorHandoffCreated ev, CancellationToken ct)
    {
        if (!ValidIdentity(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.CreatedAt) ||
            string.IsNullOrWhiteSpace(ev.Purpose) || ev.Purpose.Trim() != ev.Purpose ||
            !ValidSelection(ev.RecordVersions) ||
            (ev.ComparedToHandoffId is { } comparedTo &&
             (comparedTo == Uuid.Empty || comparedTo == ev.HandoffId)) ||
            ev.ExpiresOn < DateOnly.FromDateTime(ev.CreatedAt.UtcDateTime) || !ValidActor(ev.CreatedBy))
            throw InvalidEvent();
        var metadata = ev.Metadata;
        if (metadata.EventId == Uuid.Empty || metadata.AggregateId != ev.HandoffId ||
            metadata.AggregateVersion != 1)
            throw InvalidEvent();

        var current = await AdvisorHandoffDirectorySchema.Handoffs.GetAsync(Transaction,
            ev.HandoffId, ct).ConfigureAwait(false);
        if (current is not null)
        {
            RequireScope(current, ev.TenantId, ev.ProgramId, ev.HandoffId);
            if (IsAlreadyProjected(current, ev))
                return;
            throw new InvalidOperationException("An advisor handoff identity already exists.");
        }

        await AdvisorHandoffDirectorySchema.Handoffs.InsertAsync(Transaction,
            new AdvisorHandoffDirectoryEntry(ev.TenantId, ev.ProgramId, ev.HandoffId,
                ev.Purpose, "draft", ev.CreatedAt, ev.ExpiresOn, 1,
                ev.RecordVersions.ToArray(), [], metadata.EventId,
                metadata.AggregateVersion, ev.ComparedToHandoffId), ct)
            .ConfigureAwait(false);
    }

    async ValueTask ApplyFeedbackRecordedAsync(AdvisorFeedbackRecorded ev, CancellationToken ct)
    {
        var source = ev.SelectedSource ?? new AdvisorRecordVersion(ev.RecordId, ev.RecordVersionId);
        if (!ValidIdentity(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.CreatedAt) ||
            ev.FeedbackId == Uuid.Empty || ev.RecordId == Uuid.Empty ||
            source.RecordId != ev.RecordId || source.VersionId != ev.RecordVersionId ||
            !source.HasValidIdentity || ev.Kind is not ("comment" or "change_request" or "validated") ||
            string.IsNullOrWhiteSpace(ev.Body) || ev.Body.Trim() != ev.Body || !ValidActor(ev.Author))
            throw InvalidEvent();

        var current = await GetCurrentAsync(ev.TenantId, ev.ProgramId, ev.HandoffId, ct)
            .ConfigureAwait(false);
        if (IsAlreadyProjected(current, ev))
            return;
        if (current.Status != "ready_for_review" ||
            current.ExpiresOn < DateOnly.FromDateTime(ev.CreatedAt.UtcDateTime) ||
            !current.Selection.Contains(source) ||
            current.FeedbackIds.Contains(ev.FeedbackId))
            throw InvalidEvent();
        var revised = current with
        {
            UpdatedAt = ev.CreatedAt,
            Revision = current.Revision + 1,
            FeedbackIds = [.. current.FeedbackIds, ev.FeedbackId],
        };
        await ReplaceAsync(current, WithEventMetadata(revised, ev), ct).ConfigureAwait(false);
    }

    async ValueTask ApplyFeedbackCorrectedAsync(AdvisorFeedbackCorrected ev, CancellationToken ct)
    {
        if (!ValidIdentity(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.CorrectedAt) ||
            ev.FeedbackId == Uuid.Empty || ev.CorrectionId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.CorrectedBody) || ev.CorrectedBody.Trim() != ev.CorrectedBody ||
            string.IsNullOrWhiteSpace(ev.CorrectionReason) || ev.CorrectionReason.Trim() != ev.CorrectionReason ||
            !ValidActor(ev.CorrectedBy))
            throw InvalidEvent();
        var current = await GetCurrentAsync(ev.TenantId, ev.ProgramId, ev.HandoffId, ct)
            .ConfigureAwait(false);
        if (IsAlreadyProjected(current, ev))
            return;
        if (!current.FeedbackIds.Contains(ev.FeedbackId))
            throw InvalidEvent();
        await TouchAsync(current, ev.CorrectedAt, ev, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyFeedbackRespondedAsync(AdvisorFeedbackResponded ev, CancellationToken ct)
    {
        if (!ValidIdentity(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.RespondedAt) ||
            ev.FeedbackId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.Response) ||
            ev.Response.Trim() != ev.Response || !ValidActor(ev.RespondedBy))
            throw InvalidEvent();
        var current = await GetCurrentAsync(ev.TenantId, ev.ProgramId, ev.HandoffId, ct)
            .ConfigureAwait(false);
        if (IsAlreadyProjected(current, ev))
            return;
        if (!current.FeedbackIds.Contains(ev.FeedbackId))
            throw InvalidEvent();
        await TouchAsync(current, ev.RespondedAt, ev, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyFeedbackResolvedAsync(AdvisorFeedbackResolved ev, CancellationToken ct)
    {
        if (!ValidIdentity(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.ResolvedAt) ||
            ev.FeedbackId == Uuid.Empty || !ValidActor(ev.ResolvedBy))
            throw InvalidEvent();
        var current = await GetCurrentAsync(ev.TenantId, ev.ProgramId, ev.HandoffId, ct)
            .ConfigureAwait(false);
        if (IsAlreadyProjected(current, ev))
            return;
        if (!current.FeedbackIds.Contains(ev.FeedbackId))
            throw InvalidEvent();
        await TouchAsync(current, ev.ResolvedAt, ev, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyLifecycleAsync(DomainEvent domainEvent, Uuid tenantId, Uuid programId, Uuid handoffId,
        long revision, DateTimeOffset occurredAt, string status,
        string[] allowedStatuses, CancellationToken ct,
        IReadOnlyList<AdvisorRecordVersion>? selection = null)
    {
        var current = await GetCurrentAsync(tenantId, programId, handoffId, ct)
            .ConfigureAwait(false);
        if (IsAlreadyProjected(current, domainEvent))
            return;
        if (!allowedStatuses.Contains(current.Status) || revision != current.Revision + 1 ||
            occurredAt < current.UpdatedAt || status == "ready_for_review" &&
            current.ExpiresOn < DateOnly.FromDateTime(occurredAt.UtcDateTime) || selection is not null &&
            current.Selection.SequenceEqual(selection))
            throw InvalidEvent();
        var revised = current with
        {
            Status = status,
            UpdatedAt = occurredAt,
            Revision = revision,
            Selection = selection?.ToArray() ?? current.Selection,
        };
        await ReplaceAsync(current, WithEventMetadata(revised, domainEvent), ct)
            .ConfigureAwait(false);
    }

    async ValueTask TouchAsync(AdvisorHandoffDirectoryEntry current, DateTimeOffset occurredAt,
        DomainEvent domainEvent, CancellationToken ct)
    {
        if (occurredAt < current.UpdatedAt)
            throw InvalidEvent();
        var revised = current with
        {
            UpdatedAt = occurredAt,
            Revision = current.Revision + 1,
        };
        await ReplaceAsync(current, WithEventMetadata(revised, domainEvent), ct)
            .ConfigureAwait(false);
    }

    async ValueTask<AdvisorHandoffDirectoryEntry> GetCurrentAsync(Uuid tenantId,
        Uuid programId, Uuid handoffId, CancellationToken ct)
    {
        var current = await AdvisorHandoffDirectorySchema.Handoffs.GetAsync(Transaction,
            handoffId, ct).ConfigureAwait(false);
        if (current is null)
            throw new InvalidOperationException("An advisor handoff event projected before creation.");
        RequireScope(current, tenantId, programId, handoffId);
        return current;
    }

    ValueTask ReplaceAsync(AdvisorHandoffDirectoryEntry current,
        AdvisorHandoffDirectoryEntry revised, CancellationToken ct) =>
        AdvisorHandoffDirectorySchema.Handoffs.ReplaceAsync(Transaction, current, revised, ct);

    public async ValueTask<Page<AdvisorRequestSummaryView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var page = await AdvisorHandoffDirectorySchema.Handoffs.QueryAsync(tx,
            AdvisorHandoffDirectorySchema.ByProgramUpdatedAt.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
        return new Page<AdvisorRequestSummaryView>(page.Items.Select(static item =>
            new AdvisorRequestSummaryView(item.TenantId, item.ProgramId, item.AdvisorRequestId,
                item.Name, item.Status, item.UpdatedAt, item.ComparedToHandoffId)).ToArray(),
            page.NextCursor);
    }

    public async ValueTask<Page<AdvisorRequestSummaryView>> ListSharedProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly asOf, int limit, string? cursor, CancellationToken ct = default)
    {
        string route;
        await using (var routeTx = await BeginReadAsync(tenantId.ToString(), ct)
                         .ConfigureAwait(false))
            route = routeTx.Route;
        string? backfillCursor = null;
        do
        {
            var batch = await AdvisorHandoffDirectorySchema.Handoffs.BackfillAsync(_directoryClient,
                route, AdvisorHandoffDirectorySchema.ByProgramStatusUpdatedAt, 200,
                KvDurability.Sync, backfillCursor, ct).ConfigureAwait(false);
            backfillCursor = batch.NextCursor;
        } while (backfillCursor is not null);

        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var pageSize = Math.Clamp(limit, 1, 200);
        var items = new List<AdvisorRequestSummaryView>(pageSize);
        var nextCursor = cursor;
        while (items.Count < pageSize)
        {
            var remaining = pageSize - items.Count;
            var page = await AdvisorHandoffDirectorySchema.Handoffs.QueryAsync(tx,
                AdvisorHandoffDirectorySchema.ByProgramStatusUpdatedAt.Query()
                    .WithPrefix(programId.ToString(), "ready_for_review")
                    .Take(remaining).After(nextCursor), ct).ConfigureAwait(false);
            items.AddRange(page.Items.Where(item => item.ExpiresOn >= asOf)
                .Select(ToSummary));
            nextCursor = page.NextCursor;
            if (nextCursor is null || page.Items.Count == 0)
                break;
        }
        return new Page<AdvisorRequestSummaryView>(items, nextCursor);
    }

    static AdvisorRequestSummaryView ToSummary(AdvisorHandoffDirectoryEntry item) =>
        new(item.TenantId, item.ProgramId, item.AdvisorRequestId, item.Name,
            item.Status, item.UpdatedAt, item.ComparedToHandoffId);

    static bool ValidIdentity(Uuid tenantId, Uuid programId, Uuid handoffId,
        DateTimeOffset occurredAt) => tenantId != Uuid.Empty && programId != Uuid.Empty &&
        handoffId != Uuid.Empty && occurredAt != default;

    static bool ValidActor(ActorReference? actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Kind) && !string.IsNullOrWhiteSpace(actor.Id) &&
        !string.IsNullOrWhiteSpace(actor.Display);

    static bool IsAlreadyProjected(AdvisorHandoffDirectoryEntry current, DomainEvent domainEvent)
    {
        var metadata = domainEvent.Metadata;
        if (metadata.EventId == Uuid.Empty || metadata.AggregateId != current.AdvisorRequestId ||
            metadata.AggregateVersion == 0)
            throw InvalidEvent();
        if (metadata.AggregateVersion == current.LastAggregateVersion &&
            metadata.EventId == current.LastEventId)
            return true;
        if (metadata.AggregateVersion <= current.LastAggregateVersion)
            throw InvalidEvent();
        if (metadata.AggregateVersion != current.LastAggregateVersion + 1)
            throw InvalidEvent();
        return false;
    }

    static AdvisorHandoffDirectoryEntry WithEventMetadata(AdvisorHandoffDirectoryEntry revised,
        DomainEvent domainEvent) => revised with
        {
            LastEventId = domainEvent.Metadata.EventId,
            LastAggregateVersion = domainEvent.Metadata.AggregateVersion,
        };

    static bool ValidSelection(IReadOnlyList<AdvisorRecordVersion>? selection) =>
        selection is { Count: > 0 and <= AdvisorHandoff.MaximumSelectionCount } &&
        selection.All(static source => source is not null && source.HasValidIdentity) &&
        selection.Select(static source => (source.SourceFamily, source.RecordId, source.EntryId))
            .Distinct().Count() == selection.Count;

    static void RequireScope(AdvisorHandoffDirectoryEntry current, Uuid tenantId,
        Uuid programId, Uuid handoffId)
    {
        if (current.TenantId != tenantId || current.ProgramId != programId ||
            current.AdvisorRequestId != handoffId)
            throw new InvalidOperationException("The advisor handoff projection identity changed.");
    }

    static InvalidOperationException InvalidEvent() =>
        new("The advisor handoff directory event is invalid or out of order.");
}
