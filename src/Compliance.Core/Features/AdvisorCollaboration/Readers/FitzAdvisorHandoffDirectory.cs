using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;

sealed class FitzAdvisorHandoffDirectory : FitzKvProjectionStore,
    IAdvisorHandoffDirectoryReader, IAdvisorHandoffDirectoryProjection
{
    public const string ProjectorName = "AdvisorHandoffDirectoryV1";
    readonly IKvClient _directoryClient;
    readonly IImmutableContentStore _content;
    Uuid? _batchTenantId;

    public FitzAdvisorHandoffDirectory(IKvClient client, IImmutableContentStore content)
        : base(client, "kv://bdgrz/advisor-handoff-directory/projection-v1", ProjectorName)
    {
        _directoryClient = client;
        _content = content;
    }

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

    public async ValueTask ApplyAsync(DomainEvent domainEvent, IReadOnlyList<AdvisorRecordVersion>? selection,
        CancellationToken ct = default)
    {
        EnsureTenant(domainEvent switch
        {
            AdvisorHandoffCreated ev => ev.TenantId,
            AdvisorHandoffSelectionRevised ev => ev.TenantId,
            AdvisorHandoffReadiedForReview ev => ev.TenantId,
            AdvisorHandoffClosed ev => ev.TenantId,
            AdvisorHandoffRevoked ev => ev.TenantId,
            AdvisorFeedbackAdmitted ev => TenantOf(ev),
            _ => throw InvalidEvent(),
        });
        switch (domainEvent)
        {
            case AdvisorHandoffCreated created:
                var creation = await AdvisorAuthored.ReadAsync(_content, created, ct).ConfigureAwait(false);
                await ApplyCreatedAsync(created, creation, selection ?? throw InvalidEvent(), ct).ConfigureAwait(false);
                break;
            case AdvisorHandoffSelectionRevised revised:
                var revision = await AdvisorAuthored.ReadAsync(_content, revised, ct).ConfigureAwait(false);
                if (!ValidIdentity(revised.TenantId, revised.ProgramId, revised.HandoffId,
                        revised.RevisedAt) ||
                    !ValidSelection(selection) || !selection!.SequenceEqual(revision.RecordVersions) ||
                    string.IsNullOrWhiteSpace(revision.ChangeReason) ||
                    revision.ChangeReason.Trim() != revision.ChangeReason || !ValidActor(revised.RevisedBy))
                    throw InvalidEvent();
                await ApplyLifecycleAsync(revised, revised.TenantId, revised.ProgramId, revised.HandoffId,
                    revised.Revision, revised.RevisedAt, "draft", ["draft", "ready_for_review"],
                    ct, selection)
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
                var closure = await AdvisorAuthored.ReadAsync(_content, closed, ct).ConfigureAwait(false);
                if (!ValidIdentity(closed.TenantId, closed.ProgramId, closed.HandoffId,
                        closed.ClosedAt) || string.IsNullOrWhiteSpace(closure.Rationale) ||
                    closure.Rationale.Trim() != closure.Rationale || !ValidActor(closed.ClosedBy))
                    throw InvalidEvent();
                await ApplyLifecycleAsync(closed, closed.TenantId, closed.ProgramId, closed.HandoffId,
                    closed.Revision, closed.ClosedAt, "closed", ["draft", "ready_for_review"], ct)
                    .ConfigureAwait(false);
                break;
            case AdvisorHandoffRevoked revoked:
                var revocation = await AdvisorAuthored.ReadAsync(_content, revoked, ct).ConfigureAwait(false);
                if (!ValidIdentity(revoked.TenantId, revoked.ProgramId, revoked.HandoffId,
                        revoked.RevokedAt) || string.IsNullOrWhiteSpace(revocation.Rationale) ||
                    revocation.Rationale.Trim() != revocation.Rationale || !ValidActor(revoked.RevokedBy))
                    throw InvalidEvent();
                await ApplyLifecycleAsync(revoked, revoked.TenantId, revoked.ProgramId, revoked.HandoffId,
                    revoked.Revision, revoked.RevokedAt, "revoked", ["draft", "ready_for_review"], ct)
                    .ConfigureAwait(false);
                break;
            case AdvisorFeedbackAdmitted admitted:
                await ApplyFeedbackAdmittedAsync(admitted, ct).ConfigureAwait(false);
                break;
        }
    }

    static Uuid TenantOf(AdvisorFeedbackAdmitted ev) =>
        Uuid.TryParse(ev.Owner.Realm, System.Globalization.CultureInfo.InvariantCulture, out var tenantId)
            ? tenantId
            : throw InvalidEvent();

    void EnsureTenant(Uuid tenantId)
    {
        if (_batchTenantId != tenantId)
            throw new InvalidOperationException(
                "An advisor handoff event must belong to its tenant projection batch.");
    }

    async ValueTask ApplyCreatedAsync(AdvisorHandoffCreated ev, CreateAdvisorHandoff authored,
        IReadOnlyList<AdvisorRecordVersion> selection,
        CancellationToken ct)
    {
        if (!ValidIdentity(ev.TenantId, ev.ProgramId, ev.HandoffId, ev.CreatedAt) ||
            string.IsNullOrWhiteSpace(authored.Purpose) || authored.Purpose.Trim() != authored.Purpose ||
            !ValidSelection(selection) || !selection.SequenceEqual(authored.RecordVersions) ||
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
                authored.Purpose, "draft", ev.CreatedAt, ev.ExpiresOn, 1,
                selection.ToArray(), [], metadata.EventId,
                metadata.AggregateVersion, ev.ComparedToHandoffId), ct)
            .ConfigureAwait(false);
    }

    async ValueTask ApplyFeedbackAdmittedAsync(AdvisorFeedbackAdmitted ev, CancellationToken ct)
    {
        var handoffId = Uuid.TryParse(ev.Owner.Resource, System.Globalization.CultureInfo.InvariantCulture,
            out var parsed) ? parsed : throw InvalidEvent();
        var feedbackId = ev.Operation.LogicalId;
        if (ev.Owner.Area != AdvisorHandoff.Area || feedbackId == Uuid.Empty || handoffId == Uuid.Empty ||
            ev.Operation.AcceptedAt == default)
            throw InvalidEvent();
        var current = await AdvisorHandoffDirectorySchema.Handoffs.GetAsync(Transaction, handoffId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
                "An advisor handoff event projected before creation.");
        if (IsAlreadyProjected(current, ev))
            return;
        if (current.Status != "ready_for_review" || ev.Revision != current.Revision + 1 ||
            current.ExpiresOn < DateOnly.FromDateTime(ev.Operation.AcceptedAt.UtcDateTime) ||
            current.FeedbackIds.Contains(feedbackId) || ev.Operation.AcceptedAt < current.UpdatedAt)
            throw InvalidEvent();
        var revised = current with
        {
            UpdatedAt = ev.Operation.AcceptedAt,
            Revision = ev.Revision,
            FeedbackIds = [.. current.FeedbackIds, feedbackId],
        };
        await ReplaceAsync(current, WithEventMetadata(revised, ev), ct).ConfigureAwait(false);
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
