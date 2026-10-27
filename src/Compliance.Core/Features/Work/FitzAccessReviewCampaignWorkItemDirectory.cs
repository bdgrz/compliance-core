using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzAccessReviewCampaignWorkItemDirectory(IKvClient client,
    IAggregateReader sourceReader, IImmutableContentStore content, IDomainEventReader events)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/access-review-campaigns-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IAccessReviewCampaignWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemAccessReviewCampaignV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => AccessReviewCampaignWork.ProjectedKinds;

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), AccessReviewItem.Area);

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await AccessReviewCampaignWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var (tenantId, campaignId, itemId, eventRevision) = ScopeFor(domainEvent);
        if (tenantId == Uuid.Empty || campaignId == Uuid.Empty || itemId == Uuid.Empty)
            throw new InvalidOperationException(
                "An access-review item work event must have complete tenant, campaign and item scope.");

        var item = await sourceReader.HydrateAsync(new AccessReviewItem(tenantId, campaignId, itemId), ct)
            .ConfigureAwait(false);
        var campaign = await sourceReader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId), ct)
            .ConfigureAwait(false);
        if (item.Opened is null || item.Revision < eventRevision || item.Revision < 1 ||
            campaign.Launched is not { } launched || launched.TenantId != tenantId ||
            launched.CampaignId != campaignId)
            throw new InvalidOperationException(
                "An access-review work event must match its authoritative source aggregates.");

        var current = await AccessReviewCampaignWorkItemDirectorySchema.Items.GetAsync(Transaction,
            AccessReviewCampaignWorkItemDirectorySchema.ItemKey(campaignId, itemId), ct).ConfigureAwait(false);
        if (current is not null && (current.TenantId != tenantId || current.CampaignId != campaignId ||
                                    current.ItemId != itemId))
            throw new InvalidOperationException(
                "An access-review item cannot replace work state outside its recorded scope.");

        if (launched.ProgramId is null && launched.RemediationOwnerMemberId is null)
        {
            if (current is not null)
                throw new InvalidOperationException(
                    "A routed access-review campaign cannot lose its frozen Program or remediation owner.");
            return;
        }

        if (launched.ProgramId is not { } programId ||
            launched.RemediationOwnerMemberId is not { } remediationOwnerId)
            throw new InvalidOperationException(
                "A routed access-review campaign must freeze both its Program and remediation owner.");

        if (programId == Uuid.Empty || remediationOwnerId == Uuid.Empty)
            throw new InvalidOperationException(
                "A routed access-review campaign has incomplete work projection state.");

        var program = await sourceReader.HydrateAsync(new ComplianceProgram(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (!program.IsCreated)
            throw new InvalidOperationException(
                "A routed access-review campaign must reference a created Program in its tenant.");

        if (current is not null && current.ProgramId != programId)
            throw new InvalidOperationException(
                "A campaign's frozen Program cannot change after launch.");

        var frozen = await AccessReviewItemFold.FrozenItemAsync(item, content, tenantId, ct).ConfigureAwait(false);
        var history = await AccessReviewItemHistory.ReadAsync(events, item, ct).ConfigureAwait(false);
        var folded = await AccessReviewItemFold.ReadAsync(item, history, frozen, remediationOwnerId, content, tenantId, ct)
            .ConfigureAwait(false);
        if (frozen.ItemId == Uuid.Empty || frozen.SystemInstanceId == Uuid.Empty)
            throw new InvalidOperationException("A frozen campaign item has incomplete work scope.");
        var next = new AccessReviewCampaignWorkItemState(tenantId, programId, campaignId, itemId, item.Revision,
            DateOnly.FromDateTime(launched.Deadline.UtcDateTime), launched.LaunchedAt, item.IsClosed,
            frozen.SystemInstanceId,
            frozen.SubjectUserId is { } subjectUserId ? RbacIds.Member(tenantId, subjectUserId) : null,
            frozen.Privileged, folded.CurrentDecision?.Decision, folded.Verification is not null,
            folded.Exception is not null, folded.Exception?.ExpiresAt, folded.CurrentReviewerMemberId,
            folded.CurrentRemediationOwnerMemberId, folded.Changes.Count > 0);
        // The state is always folded from the authoritative item, so a replay or an older event reproduces the current
        // state and changes nothing. A published bulk decision changes the state without advancing the revision.
        if (current == next)
            return;
        if (current is null)
            await AccessReviewCampaignWorkItemDirectorySchema.Items.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await AccessReviewCampaignWorkItemDirectorySchema.Items.ReplaceAsync(Transaction, current, next, ct)
                .ConfigureAwait(false);

        await IncrementRevisionAsync(ct).ConfigureAwait(false);
    }

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default) =>
        LoadProgramAsync(tenantId, programId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null, ct);

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly today, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default) => LoadProgramAsync(tenantId, programId,
        new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), horizon,
        workItemId, ct);

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateTimeOffset now, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await AccessReviewCampaignWorkItemDirectorySchema.Items.QueryAsync(tx,
                AccessReviewCampaignWorkItemDirectorySchema.ItemsByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var state in page.Items)
            {
                if (state.TenantId != tenantId || state.ProgramId != programId ||
                    state.CampaignId == Uuid.Empty || state.ItemId == Uuid.Empty ||
                    state.SourceRevision < 1)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                if (state.IsCompleted || state.DueOn > horizon)
                    continue;

                candidates.AddRange(AccessReviewCampaignWork.Candidates(state, now, workItemId));
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await AccessReviewCampaignWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await AccessReviewCampaignWorkItemDirectorySchema.Revisions.InsertAsync(Transaction,
                next, ct).ConfigureAwait(false);
        else
            await AccessReviewCampaignWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static (Uuid TenantId, Uuid CampaignId, Uuid ItemId, long Revision) ScopeFor(DomainEvent domainEvent) =>
        domainEvent switch
        {
            AccessReviewItemOpened ev => (ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision),
            AccessDecisionsRecorded ev => (ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision),
            AccessRemediationChangeRecorded ev => (ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision),
            AccessRemediationVerified ev => (ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision),
            AccessRemediationExceptionRecorded ev => (ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision),
            AccessReviewResponsibilityReassigned ev => (ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision),
            AccessReviewItemCampaignCompletionUseSettled ev => (ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision),
            AccessReviewItemDecisionSettled ev => (ev.TenantId, ev.CampaignId, ev.ItemId, ev.Revision),
            _ => (Uuid.Empty, Uuid.Empty, Uuid.Empty, 0),
        };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The access-review campaign work projection has an invalid Program scope.");
}
