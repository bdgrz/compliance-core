using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures each routed access-review campaign's history on the Type I date.</summary>
public sealed class AccessReviewCampaignsTypeIBaselineSourceContributor(
    IAccessReviewCampaignDirectoryReader directory, IAggregateReader reader,
    IDomainEventReader events, AccessReviewReader campaigns, IImmutableContentStore content)
    : ITypeIBaselineSourceContributor
{
    const int PageSize = 200;
    const string DigestDomain = "bdgrz.snapshot.source.access_review_campaign.v1\0";

    public string Family => "access_review_campaigns";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null ||
            engagement.TenantId == Uuid.Empty || engagement.ProgramId == Uuid.Empty ||
            engagement.PeriodStart == default)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "An access review campaign baseline read requires its engagement and source context."));

        var fence = await directory.LoadCheckpointAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (await HasPendingSourceAsync(engagement.TenantId, fence, ct).ConfigureAwait(false))
            return BehindSource();

        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var summaries = new List<AccessReviewCampaignSummaryView>();
        var seenCampaignIds = new HashSet<Uuid>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await directory.ListAsync(engagement.TenantId, PageSize, cursor, ct)
                .ConfigureAwait(false);
            if (page.Items is null || page.Items.Count > PageSize)
                return InvalidDirectory();

            foreach (var summary in page.Items)
            {
                if (!ValidSummary(summary, engagement.TenantId) ||
                    !seenCampaignIds.Add(summary.CampaignId))
                    return InvalidDirectory();
                summaries.Add(summary);
            }

            var next = page.NextCursor;
            if (next is not null && (next == cursor || !seenCursors.Add(next)))
                return InvalidDirectory();
            cursor = next;
        } while (cursor is not null);

        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var summary in summaries.OrderBy(static summary =>
                     summary.CampaignId.ToString(), StringComparer.Ordinal))
        {
            if (summary.LaunchedAt > through)
                continue;

            var stream = new EventStreamAddress(engagement.TenantId.ToString(),
                AccessReviewCampaign.Area, summary.CampaignId.ToString());
            var history = await ReadHistoryAsync(stream, engagement.TenantId, summary.CampaignId,
                through, summary, ct).ConfigureAwait(false);
            if (!history.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
            if (!await VerifyLaunchSnapshotAsync(engagement.TenantId, history.Value.Launch, ct)
                    .ConfigureAwait(false) ||
                history.Value.Completion is { } completion &&
                !await VerifyCompletionSnapshotAsync(engagement.TenantId, history.Value.Snapshot,
                    history.Value.Launch, completion, ct).ConfigureAwait(false))
                return SourceConflict();

            if (history.Value.AsOfRevision == 0)
            {
                scopeDifferences.Add(
                    $"Access review campaign {summary.CampaignId} was not recorded on the Type I date.");
                continue;
            }

            if (history.Value.Launch.ProgramId is null)
            {
                scopeDifferences.Add(
                    $"Access review campaign {summary.CampaignId} has no routed owner Program and cannot be assigned to this engagement.");
                continue;
            }
            if (history.Value.Launch.ProgramId != engagement.ProgramId)
                continue;

            try
            {
                var content = new AccessReviewCampaignSourceContent(engagement.PeriodStart,
                    history.Value.AsOfEvents.AsReadOnly());
                sources.Add(new TypeIBaselineSourceReference("access_review_campaign",
                    summary.CampaignId, Uuid.CreateVersion5(summary.CampaignId,
                        "access_review_campaign_v1:" + engagement.PeriodStart.ToString(
                            "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
                    history.Value.AsOfRevision, Digest(content)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                InvalidOperationException or NotSupportedException)
            {
                blockers.Add(
                    $"Access review campaign {summary.CampaignId} has invalid canonical source content.");
                scopeDifferences.Add(
                    $"Access review campaign {summary.CampaignId} has invalid canonical source content.");
            }
        }

        var confirmed = await directory.LoadCheckpointAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (confirmed != fence ||
            await HasPendingSourceAsync(engagement.TenantId, confirmed, ct).ConfigureAwait(false))
            return BehindSource();

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(),
                blockers.AsReadOnly(), scopeDifferences.AsReadOnly()));
    }

    async ValueTask<Result<AccessReviewCampaignHistory>> ReadHistoryAsync(
        EventStreamAddress stream, Uuid tenantId, Uuid campaignId, DateTimeOffset through,
        AccessReviewCampaignSummaryView summary, CancellationToken ct)
    {
        try
        {
            return await ReadHistoryCoreAsync(stream, tenantId, campaignId, through, summary, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException)
        {
            return InvalidHistory();
        }
    }

    async ValueTask<Result<AccessReviewCampaignHistory>> ReadHistoryCoreAsync(
        EventStreamAddress stream, Uuid tenantId, Uuid campaignId, DateTimeOffset through,
        AccessReviewCampaignSummaryView summary, CancellationToken ct)
    {
        var seenEventIds = new HashSet<Uuid>();
        var usedDecisionIds = new HashSet<Uuid>();
        var usedReassignmentIds = new HashSet<Uuid>();
        var asOf = new List<AsOfEvent>();
        var states = new Dictionary<Uuid, CampaignItemHistory>();
        var aggregate = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId), ct)
            .ConfigureAwait(false);
        if (aggregate.Launched is not { } launched || launched.TenantId != tenantId ||
            launched.CampaignId != campaignId)
            return InvalidHistory();
        var launch = await campaigns.LaunchOfAsync(tenantId, launched, ct).ConfigureAwait(false);
        if (!ValidLaunch(launch, campaignId))
            return InvalidHistory();
        foreach (var item in launch.Items)
            states.Add(item.ItemId, new CampaignItemHistory(item, launch.RemediationOwnerMemberId));

        AccessReviewCampaignCompletionView? completion = null;
        ulong expectedVersion = 1;
        DateTimeOffset? priorRecordedAt = null;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            var domainEvent = record.Event;
            var metadata = domainEvent?.Metadata;
            if (record.Stream != stream || domainEvent is null || metadata is null ||
                metadata.EventId == Uuid.Empty || !seenEventIds.Add(metadata.EventId) ||
                metadata.AggregateId != campaignId || metadata.AggregateVersion != expectedVersion ||
                metadata.OccurredOn == default ||
                priorRecordedAt is { } earlierRecord && metadata.OccurredOn < earlierRecord)
                return InvalidHistory();
            switch (domainEvent)
            {
                case AccessReviewCampaignLaunched when expectedVersion == 1:
                    Include(asOf, launch.LaunchedAt, metadata, "campaign_launched", stream,
                        JsonSerializer.SerializeToElement(launch, ComplianceCoreJsonContext.Default.AccessReviewLaunch),
                        through);
                    break;
                case AccessReviewCampaignCompletionAccepted or AccessReviewCampaignCompletionCanceled or
                    AccessReviewDecisionBatchAdmitted when expectedVersion > 1 && completion is null:
                    break;
                case AccessReviewCampaignCompleted completed when expectedVersion > 1 && completion is null:
                    completion = await AccessReviewContent.ReadAsync<AccessReviewCampaignCompletionView>(content,
                        tenantId, completed.CompletionContent, ct).ConfigureAwait(false);
                    Include(asOf, completion.CompletedAt, metadata, "campaign_completed", stream,
                        JsonSerializer.SerializeToElement(new AccessReviewCampaignCompletionSource(completed.Revision,
                            completion), ComplianceCoreJsonContext.Default.AccessReviewCampaignCompletionSource),
                        through);
                    break;
                default:
                    return InvalidHistory();
            }

            priorRecordedAt = metadata.OccurredOn;
            expectedVersion++;
        }

        if (expectedVersion == 1)
            return InvalidHistory();

        foreach (var item in launch.Items)
        {
            var replayed = await ReplayItemAsync(tenantId, campaignId, launch, item, states[item.ItemId], seenEventIds,
                usedDecisionIds, usedReassignmentIds, asOf, through, ct).ConfigureAwait(false);
            if (!replayed)
                return InvalidHistory();
        }

        if (completion is not null && !ValidCompletion(completion, launch, states))
            return InvalidHistory();
        if (!MatchesSummary(summary, launch, completion))
            return InvalidHistory();
        var snapshot = await campaigns.ReadCampaignAsync(tenantId, campaignId, ct).ConfigureAwait(false);
        if (!CurrentStateMatches(snapshot, launch, completion, states))
            return InvalidHistory();

        var ordered = asOf.OrderBy(static fact => fact.At)
            .ThenBy(static fact => fact.Stream.Resource, StringComparer.Ordinal)
            .ThenBy(static fact => fact.Event.AggregateVersion).ToList();
        return Result<AccessReviewCampaignHistory>.Success(new AccessReviewCampaignHistory(launch, completion, snapshot,
            ordered.Count, [.. ordered.Select(static fact => fact.Event)]));
    }

    async ValueTask<bool> ReplayItemAsync(Uuid tenantId, Uuid campaignId, AccessReviewLaunch launch,
        AccessReviewItemView frozen, CampaignItemHistory state, HashSet<Uuid> seenEventIds,
        HashSet<Uuid> usedDecisionIds, HashSet<Uuid> usedReassignmentIds, List<AsOfEvent> asOf,
        DateTimeOffset through, CancellationToken ct)
    {
        var stream = AccessReviewEffects.ItemStream(tenantId, campaignId, frozen.ItemId);
        var streamId = AccessReviewEffects.ItemStreamId(tenantId, campaignId, frozen.ItemId);
        ulong expectedVersion = 1;
        long expectedRevision = 0;
        DateTimeOffset? priorRecordedAt = null;
        DateTimeOffset? priorOccurredAt = null;
        var opened = false;
        try
        {
            await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
            {
                var domainEvent = record.Event;
                var metadata = domainEvent?.Metadata;
                if (record.Stream != stream || domainEvent is null || metadata is null ||
                    metadata.EventId == Uuid.Empty || !seenEventIds.Add(metadata.EventId) ||
                    metadata.AggregateId != streamId || metadata.AggregateVersion != expectedVersion ||
                    metadata.OccurredOn == default ||
                    priorRecordedAt is { } earlierRecord && metadata.OccurredOn < earlierRecord)
                    return false;
                priorRecordedAt = metadata.OccurredOn;
                expectedVersion++;
                if (domainEvent is AccessReviewItemCampaignCompletionUseAuthorized or
                    AccessReviewItemCampaignCompletionUseSettled or AccessReviewItemDecisionAuthorized or
                    AccessReviewItemDecisionCanceled)
                    continue;
                if (domainEvent is AccessReviewItemDecisionSettled settledDecision)
                {
                    // A bulk decision becomes a source event only when its batch published and settled on the item.
                    if (settledDecision.Disposition == SourceUseSettlementDisposition.IrreversibleDecision)
                    {
                        if (!state.Pending.Remove(settledDecision.Operation.RootOperationId, out var pending))
                            return false;
                        state.Decisions.Add(pending.Decision);
                        Include(asOf, pending.Decision.DecidedAt, metadata, "decisions_recorded", stream,
                            JsonSerializer.SerializeToElement(new AccessReviewItemDecisionsSource(frozen.ItemId,
                                pending.Revision, [pending.Decision]),
                                ComplianceCoreJsonContext.Default.AccessReviewItemDecisionsSource), through);
                    }
                    else
                    {
                        state.Pending.Remove(settledDecision.Operation.RootOperationId);
                    }

                    continue;
                }

                if (domainEvent is AccessReviewItemOpened itemOpened)
                {
                    var intent = await AccessReviewContent.ReadAsync<AccessReviewItemCreationIntent>(content, tenantId,
                        itemOpened.Intent, ct).ConfigureAwait(false);
                    var entry = await AccessReviewContent.ReadAsync<AccessReviewItemView>(content, tenantId,
                        intent.PopulationEntry, ct).ConfigureAwait(false);
                    if (opened || itemOpened.Revision != 1 || !SameJson(entry, frozen,
                            ComplianceCoreJsonContext.Default.AccessReviewItemView))
                        return false;
                    opened = true;
                    expectedRevision = 1;
                    continue;
                }

                var (kind, at, payload) = await ApplyItemFactAsync(domainEvent, tenantId, campaignId, launch, state,
                    usedDecisionIds, usedReassignmentIds, ++expectedRevision, ct).ConfigureAwait(false);
                if (!opened || kind is null)
                    return false;
                if (kind == ProvisionalKind)
                    continue;
                if (priorOccurredAt is { } earlierEvent && at < earlierEvent)
                    return false;
                priorOccurredAt = at;
                Include(asOf, at, metadata, kind, stream, payload, through);
            }
        }
        catch (InvalidOperationException) when (expectedVersion == 1)
        {
            // The in-memory store reports an item stream that was never written this way.
        }

        return true;
    }

    async ValueTask<(string? Kind, DateTimeOffset At, JsonElement Payload)> ApplyItemFactAsync(DomainEvent domainEvent,
        Uuid tenantId, Uuid campaignId, AccessReviewLaunch launch, CampaignItemHistory state,
        HashSet<Uuid> usedDecisionIds, HashSet<Uuid> usedReassignmentIds, long expectedRevision, CancellationToken ct)
    {
        var json = ComplianceCoreJsonContext.Default;
        var itemId = state.Item.ItemId;
        var invalid = (default(string), default(DateTimeOffset), default(JsonElement));
        switch (domainEvent)
        {
            case AccessDecisionsRecorded decisions:
                {
                    var list = await AccessReviewContent.ReadAsync<IReadOnlyList<AccessDecisionView>>(content, tenantId,
                        decisions.DecisionsManifest, ct).ConfigureAwait(false);
                    if (decisions.TenantId != tenantId || decisions.CampaignId != campaignId ||
                        decisions.ItemId != itemId || decisions.Revision != expectedRevision ||
                        decisions.PublicationRequired != (decisions.DecisionBatchId is not null) ||
                        list.Any(decision => decision.ItemId != itemId) ||
                        !ValidDecisions(list, launch, new Dictionary<Uuid, CampaignItemHistory> { [itemId] = state },
                            usedDecisionIds))
                        return invalid;
                    if (decisions.DecisionBatchId is { } batchId)
                    {
                        // Hidden until the batch published: kept pending, counted in the item's revision.
                        if (list.Count != 1 || !state.Pending.TryAdd(batchId, new PendingDecision(list[0],
                                decisions.Revision)))
                            return invalid;
                        return (ProvisionalKind, list[0].DecidedAt, default);
                    }

                    state.Decisions.Add(list[0]);
                    return ("decisions_recorded", list[0].DecidedAt, JsonSerializer.SerializeToElement(
                        new AccessReviewItemDecisionsSource(itemId, decisions.Revision, list),
                        json.AccessReviewItemDecisionsSource));
                }
            case AccessRemediationChangeRecorded changed:
                {
                    var change = await AccessReviewContent.ReadAsync<AccessRemediationChangeView>(content, tenantId,
                        changed.ChangeContent, ct).ConfigureAwait(false);
                    if (changed.TenantId != tenantId || changed.CampaignId != campaignId || changed.ItemId != itemId ||
                        changed.Revision != expectedRevision || !RequiresRemediation(state) ||
                        change.ChangeId == Uuid.Empty || !ValidText(change.Reference, 500) ||
                        !ValidText(change.Description, 4000) || change.ChangedAt == default ||
                        change.RecordedAt == default || change.ChangedAt > change.RecordedAt ||
                        !ValidMemberActor(change.RecordedBy) ||
                        launch.ProgramId is not null &&
                        state.CurrentRemediationOwnerMemberId != ActorMemberId(change.RecordedBy) ||
                        state.Changes.Any(existing => existing.ChangeId == change.ChangeId))
                        return invalid;
                    state.Changes.Add(change);
                    return ("remediation_change_recorded", change.RecordedAt, JsonSerializer.SerializeToElement(
                        new AccessReviewItemChangeSource(itemId, changed.Revision, change),
                        json.AccessReviewItemChangeSource));
                }
            case AccessRemediationVerified verified:
                {
                    var verification = await AccessReviewContent.ReadAsync<AccessRemediationVerificationView>(content,
                        tenantId, verified.VerificationContent, ct).ConfigureAwait(false);
                    if (verified.TenantId != tenantId || verified.CampaignId != campaignId || verified.ItemId != itemId ||
                        verified.Revision != expectedRevision || !RequiresRemediation(state) ||
                        !ValidVerification(verification, state))
                        return invalid;
                    state.Verification = verification;
                    return ("remediation_verified", verification.VerifiedAt, JsonSerializer.SerializeToElement(
                        new AccessReviewItemVerificationSource(itemId, verified.Revision, verification),
                        json.AccessReviewItemVerificationSource));
                }
            case AccessRemediationExceptionRecorded excepted:
                {
                    var exception = await AccessReviewContent.ReadAsync<AccessRemediationExceptionView>(content, tenantId,
                        excepted.ExceptionContent, ct).ConfigureAwait(false);
                    if (excepted.TenantId != tenantId || excepted.CampaignId != campaignId || excepted.ItemId != itemId ||
                        excepted.Revision != expectedRevision || !RequiresRemediation(state) ||
                        !ValidException(exception, state))
                        return invalid;
                    state.Exception = exception;
                    return ("remediation_exception_recorded", exception.ApprovedAt, JsonSerializer.SerializeToElement(
                        new AccessReviewItemExceptionSource(itemId, excepted.Revision, exception),
                        json.AccessReviewItemExceptionSource));
                }
            case AccessReviewResponsibilityReassigned reassigned:
                {
                    var reassignment = await AccessReviewContent.ReadAsync<AccessReviewResponsibilityReassignmentView>(
                        content, tenantId, reassigned.ReassignmentContent, ct).ConfigureAwait(false);
                    if (reassigned.TenantId != tenantId || reassigned.CampaignId != campaignId ||
                        reassigned.ItemId != itemId || reassigned.Revision != expectedRevision ||
                        !ValidReassignment(reassignment, launch, state, usedReassignmentIds))
                        return invalid;
                    state.Reassignments.Add(reassignment);
                    if (reassignment.Responsibility == AccessReviewResponsibilityKind.Reviewer)
                        state.CurrentReviewerMemberId = reassignment.AssignedMemberId;
                    else
                        state.CurrentRemediationOwnerMemberId = reassignment.AssignedMemberId;
                    return ("responsibility_reassigned", reassignment.ReassignedAt, JsonSerializer.SerializeToElement(
                        new AccessReviewItemReassignmentSource(itemId, reassigned.Revision, reassignment),
                        json.AccessReviewItemReassignmentSource));
                }
            default:
                return invalid;
        }
    }

    static void Include(List<AsOfEvent> asOf, DateTimeOffset at, DomainEventMetadata metadata, string kind,
        EventStreamAddress stream, JsonElement payload, DateTimeOffset through)
    {
        if (at <= through && metadata.OccurredOn <= through)
            asOf.Add(new AsOfEvent(at, stream, new AccessReviewCampaignSourceEvent(metadata.EventId,
                metadata.AggregateVersion, metadata.OccurredOn, kind, payload)));
    }

    static bool ValidLaunch(AccessReviewLaunch launch, Uuid campaignId)
    {
        if (launch.CampaignId != campaignId ||
            !ValidText(launch.Name, 200) || !ValidText(launch.Instructions, 8000) ||
            launch.Deadline <= launch.LaunchedAt || launch.LaunchedAt == default ||
            launch.SnapshotId == Uuid.Empty || !IsSha256(launch.ContentSha256) ||
            !ValidMemberActor(launch.LaunchedBy) || launch.Reviewers is not { Count: > 0 } ||
            launch.Items is not { Count: > 0 } ||
            launch.ProgramId == Uuid.Empty || launch.RemediationOwnerMemberId == Uuid.Empty ||
            (launch.ProgramId is null) != (launch.RemediationOwnerMemberId is null))
            return false;

        if (launch.Reviewers.Any(reviewer => reviewer is null ||
                reviewer.PopulationId == Uuid.Empty || reviewer.SystemInstanceId == Uuid.Empty ||
                reviewer.ReviewerMemberId == Uuid.Empty ||
                reviewer.Delegated != (reviewer.DelegationReason is not null) ||
                reviewer.Delegated && !ValidText(reviewer.DelegationReason, 2000)) ||
            launch.Reviewers.Select(static reviewer => reviewer.PopulationId).Distinct().Count() !=
            launch.Reviewers.Count)
            return false;

        var reviewers = launch.Reviewers.ToDictionary(static reviewer => reviewer.PopulationId);
        var itemIds = new HashSet<Uuid>();
        var populationAccess = new HashSet<(Uuid PopulationId, string SubjectId,
            string EntitlementId)>();
        foreach (var item in launch.Items)
        {
            if (item is null || item.ItemId == Uuid.Empty || item.PopulationId == Uuid.Empty ||
                item.PopulationSnapshotId == Uuid.Empty || item.SystemInstanceId == Uuid.Empty ||
                item.ReviewerMemberId == Uuid.Empty ||
                !ValidText(item.ProviderSubjectId, 512) ||
                !AccessReviewVocabulary.PrincipalKinds.Contains(item.PrincipalKind) ||
                !AccessReviewVocabulary.IsBoundedText(item.SubjectDisplayName, 500) ||
                !AccessReviewVocabulary.Classifications.Contains(item.Classification) ||
                item.SubjectPersonId == Uuid.Empty || item.SubjectUserId == Uuid.Empty ||
                item.Classification == AccessReviewVocabulary.Human &&
                item.SubjectPersonId is null ||
                item.Classification != AccessReviewVocabulary.Human &&
                (item.SubjectPersonId is not null || item.SubjectUserId is not null) ||
                !ValidText(item.ProviderEntitlementId, 512) ||
                !AccessReviewVocabulary.EntitlementKinds.Contains(item.EntitlementKind) ||
                !AccessReviewVocabulary.IsBoundedText(item.EntitlementDisplayName, 500) ||
                item.VarianceCategory is not (AccessVarianceEvaluator.Expected or
                    AccessVarianceEvaluator.Unexpected or AccessVarianceEvaluator.Prohibited or
                    AccessVarianceEvaluator.Missing or AccessVarianceEvaluator.Unresolved or
                    AccessVarianceEvaluator.Excepted) ||
                item.Paths is not { Count: > 0 } ||
                item.Paths.Any(path => path is null || path.Hops is not { Count: > 0 } ||
                    path.ExpiresAt is { } pathExpiresAt && pathExpiresAt == default ||
                    path.Hops.Any(hop => hop is null ||
                        hop.Kind is not ("group_member" or "role_assumption" or "access_assignment") ||
                        !ValidText(hop.From, 512) || !ValidText(hop.To, 512))) ||
                item.ExpiresAt is { } itemExpiresAt && itemExpiresAt == default ||
                !itemIds.Add(item.ItemId) ||
                !populationAccess.Add((item.PopulationId, item.ProviderSubjectId,
                    item.ProviderEntitlementId)) ||
                item.ItemId != Uuid.CreateVersion5(campaignId,
                    $"{item.PopulationId}\n{item.ProviderSubjectId}\n{item.ProviderEntitlementId}") ||
                !reviewers.TryGetValue(item.PopulationId, out var reviewer) ||
                reviewer.SystemInstanceId != item.SystemInstanceId ||
                reviewer.ReviewerMemberId != item.ReviewerMemberId)
                return false;
        }

        return launch.RemediationOwnerMemberId is not { } remediationOwner ||
               launch.Reviewers.All(reviewer => reviewer.ReviewerMemberId != remediationOwner);
    }

    static bool ValidDecisions(IReadOnlyList<AccessDecisionView> decisions, AccessReviewLaunch launch,
        IReadOnlyDictionary<Uuid, CampaignItemHistory> states, HashSet<Uuid> usedDecisionIds)
    {
        if (decisions is not { Count: > 0 and <= 500 } || decisions[0] is not { } first ||
            first.DecisionId == Uuid.Empty || !usedDecisionIds.Add(first.DecisionId) ||
            !AccessReviewVocabulary.Decisions.Contains(first.Decision) ||
            !ValidText(first.Rationale, 4000) || first.DecidedAt < launch.LaunchedAt ||
            first.DecidedAt == default || first.AfterDeadline != (first.DecidedAt > launch.Deadline) ||
            !ValidMemberActor(first.DecidedBy, first.ReviewerMemberId) ||
            first.BulkPreviewToken is { } token && !IsSha256(token) ||
            first.SeparationOfDutiesWaiverId == Uuid.Empty ||
            first.BulkPreviewToken is null && decisions.Count != 1)
            return false;

        var itemIds = new HashSet<Uuid>();
        foreach (var decision in decisions)
        {
            if (decision is null || decision.DecisionId != first.DecisionId ||
                decision.Decision != first.Decision || decision.Rationale != first.Rationale ||
                decision.ReviewerMemberId != first.ReviewerMemberId ||
                decision.DecidedAt != first.DecidedAt ||
                decision.BulkPreviewToken != first.BulkPreviewToken ||
                decision.AfterDeadline != first.AfterDeadline ||
                decision.SeparationOfDutiesWaiverId != first.SeparationOfDutiesWaiverId ||
                !itemIds.Add(decision.ItemId) ||
                !states.TryGetValue(decision.ItemId, out var state) ||
                state.CurrentReviewerMemberId != decision.ReviewerMemberId ||
                !ValidMemberActor(decision.DecidedBy, decision.ReviewerMemberId))
                return false;
        }
        return true;
    }

    static bool ValidVerification(AccessRemediationVerificationView verification,
        CampaignItemHistory item) => verification.PopulationId != Uuid.Empty &&
        verification.PopulationId != item.Item.PopulationId && verification.SnapshotId != Uuid.Empty &&
        verification.CalculationId != Uuid.Empty && verification.ObservedAt != default &&
        verification.ObservedAt > item.Decisions[^1].DecidedAt &&
        verification.Outcome is "removed" or "changed" && verification.VerifiedAt >=
        verification.ObservedAt && ValidMemberActor(verification.VerifiedBy);

    static bool ValidException(AccessRemediationExceptionView exception,
        CampaignItemHistory item)
    {
        if (exception.ExceptionId == Uuid.Empty || !ValidText(exception.Rationale, 4000) ||
            exception.ApprovedAt == default ||
            exception.ExpiresAt is { } expiresAt && expiresAt <= exception.ApprovedAt ||
            !ValidMemberActor(exception.ApprovedBy))
            return false;
        return ActorMemberId(exception.ApprovedBy) != item.CurrentReviewerMemberId;
    }

    static bool ValidReassignment(AccessReviewResponsibilityReassignmentView reassignment,
        AccessReviewLaunch launch, CampaignItemHistory item,
        HashSet<Uuid> usedReassignmentIds)
    {
        if (reassignment.ReassignmentId == Uuid.Empty ||
            !AccessReviewResponsibilityKind.IsKnown(reassignment.Responsibility) ||
            reassignment.ItemId != item.Item.ItemId ||
            reassignment.AssignedMemberId == Uuid.Empty ||
            !ValidText(reassignment.Reason, 2000) || reassignment.ReassignedAt == default ||
            !ValidMemberActor(reassignment.ReassignedBy) ||
            !usedReassignmentIds.Add(reassignment.ReassignmentId))
            return false;

        if (reassignment.Responsibility == AccessReviewResponsibilityKind.Reviewer)
        {
            if (item.Decisions.Count > 0 ||
                reassignment.PreviousMemberId != item.CurrentReviewerMemberId ||
                reassignment.AssignedMemberId == item.CurrentRemediationOwnerMemberId ||
                !ValidText(reassignment.DelegationReason, 2000))
                return false;
            return true;
        }

        if (launch.ProgramId is null ||
            reassignment.PreviousMemberId != item.CurrentRemediationOwnerMemberId ||
            reassignment.AssignedMemberId == item.CurrentReviewerMemberId ||
            reassignment.DelegationReason is not null ||
            RemediationStatus(item, reassignment.ReassignedAt) is not ("pending" or "provider_changed"))
            return false;
        return true;
    }

    static bool ValidCompletion(AccessReviewCampaignCompletionView? completion,
        AccessReviewLaunch launch,
        IReadOnlyDictionary<Uuid, CampaignItemHistory> states)
    {
        if (completion is null || completion.SnapshotId == Uuid.Empty ||
            !IsSha256(completion.ContentSha256) || !ValidText(completion.Attestation, 4000) ||
            !ValidMemberActor(completion.CompletedBy) || completion.CompletedAt < launch.LaunchedAt ||
            completion.CompletedAt == default)
            return false;
        return states.Values.All(item => item.Decisions.Count > 0 &&
            RemediationStatus(item, completion.CompletedAt) is not ("pending" or "provider_changed"));
    }

    static bool CurrentStateMatches(AccessReviewCampaignSnapshot snapshot, AccessReviewLaunch launch,
        AccessReviewCampaignCompletionView? completion, IReadOnlyDictionary<Uuid, CampaignItemHistory> states)
    {
        var json = ComplianceCoreJsonContext.Default;
        if (snapshot.Id != launch.CampaignId || snapshot.Launched is null ||
            !SameJson(snapshot.Completion, completion, json.AccessReviewCampaignCompletionView))
            return false;
        var view = snapshot.ToView(DateTimeOffset.MaxValue);
        if (view.TenantId == Uuid.Empty || view.CampaignId != launch.CampaignId || view.Status != (completion is null
                ? AccessReviewCampaign.Active
                : AccessReviewCampaign.Completed) ||
            !view.Reviewers.SequenceEqual(launch.Reviewers) ||
            !view.Items.Select(static item => item.Item.ItemId).SequenceEqual(
                launch.Items.Select(static item => item.ItemId)) ||
            view.UnresolvedCount != states.Values.Count(static item => item.Decisions.Count == 0) ||
            view.RemediationOpenCount != states.Values.Count(static item =>
                RemediationStatus(item, DateTimeOffset.MaxValue) is "pending" or "provider_changed"))
            return false;

        foreach (var actual in view.Items)
        {
            if (!states.TryGetValue(actual.Item.ItemId, out var expected) ||
                !SameJson(actual.Item, expected.Item, json.AccessReviewItemView) ||
                actual.Status != (expected.Decisions.Count == 0 ? "unresolved" : "decided") ||
                actual.Decision != expected.Decisions.LastOrDefault() ||
                !actual.DecisionHistory.SequenceEqual(expected.Decisions) ||
                actual.RemediationStatus != RemediationStatus(expected, DateTimeOffset.MaxValue) ||
                !actual.ProviderChanges.SequenceEqual(expected.Changes) ||
                actual.Verification != expected.Verification || actual.Exception != expected.Exception ||
                actual.CurrentReviewerMemberId != expected.CurrentReviewerMemberId ||
                actual.CurrentRemediationOwnerMemberId != expected.CurrentRemediationOwnerMemberId ||
                actual.ResponsibilityReassignments is null ||
                !actual.ResponsibilityReassignments.SequenceEqual(expected.Reassignments))
                return false;
        }
        return true;
    }

    static bool SameJson<T>(T? left, T? right, JsonTypeInfo<T> typeInfo)
    {
        if (left is null || right is null)
            return left is null && right is null;
        var leftElement = JsonSerializer.SerializeToElement(left, typeInfo);
        var rightElement = JsonSerializer.SerializeToElement(right, typeInfo);
        using var leftStream = new MemoryStream();
        using var rightStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(leftStream, SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, leftElement);
        using (var writer = new Utf8JsonWriter(rightStream, SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, rightElement);
        return leftStream.ToArray().AsSpan().SequenceEqual(rightStream.ToArray());
    }

    async ValueTask<bool> VerifyLaunchSnapshotAsync(Uuid tenantId,
        AccessReviewLaunch launch, CancellationToken ct)
    {
        var snapshotContent = await PopulationSnapshotContent.ReadAsync(reader, tenantId, launch.SnapshotId,
            AccessReviewCampaignContent.LaunchKind, ct).ConfigureAwait(false);
        if (!snapshotContent.IsSuccess)
            return false;
        var snapshot = snapshotContent.Value.Snapshot;
        if (snapshot.Id != launch.SnapshotId || !snapshot.HasIntactIdentity ||
            snapshot.RootSnapshotId != launch.SnapshotId || snapshot.AmendsSnapshotId is not null ||
            snapshot.ContentSha256 != launch.ContentSha256)
            return false;

        var header = new AccessReviewCampaignHeader(launch.CampaignId, launch.Name,
            launch.Instructions, launch.Deadline, launch.ProgramId,
            launch.RemediationOwnerMemberId);
        var rows = AccessReviewCampaignContent.LaunchRows(header, launch.Reviewers, launch.Items);
        var digest = PopulationContentIdentity.Compute(AccessReviewCampaignContent.LaunchKind, rows);
        return digest.IsSuccess && digest.Value.Sha256 == snapshot.ContentSha256 &&
               digest.Value.RowCount == snapshot.RowCount;
    }

    async ValueTask<bool> VerifyCompletionSnapshotAsync(Uuid tenantId,
        AccessReviewCampaignSnapshot campaign, AccessReviewLaunch launch,
        AccessReviewCampaignCompletionView completion, CancellationToken ct)
    {
        var snapshotContent = await PopulationSnapshotContent.ReadAsync(reader, tenantId,
            completion.SnapshotId, AccessReviewCampaignContent.ResultKind, ct).ConfigureAwait(false);
        if (!snapshotContent.IsSuccess)
            return false;
        var snapshot = snapshotContent.Value.Snapshot;
        if (snapshot.Id != completion.SnapshotId || !snapshot.HasIntactIdentity ||
            snapshot.RootSnapshotId != completion.SnapshotId || snapshot.AmendsSnapshotId is not null ||
            snapshot.ContentSha256 != completion.ContentSha256)
            return false;

        var header = new AccessReviewCampaignResultHeader(campaign.Id, launch.Name, launch.Deadline,
            launch.SnapshotId, launch.ContentSha256, completion.Attestation);
        var rows = AccessReviewCampaignContent.ResultRows(header,
            campaign.ItemStates(completion.CompletedAt));
        var digest = PopulationContentIdentity.Compute(AccessReviewCampaignContent.ResultKind, rows);
        return digest.IsSuccess && digest.Value.Sha256 == snapshot.ContentSha256 &&
               digest.Value.RowCount == snapshot.RowCount;
    }

    static bool MatchesSummary(AccessReviewCampaignSummaryView summary,
        AccessReviewLaunch launch, AccessReviewCampaignCompletionView? completion) =>
        summary.CampaignId == launch.CampaignId &&
        summary.Name == launch.Name && summary.Deadline == launch.Deadline &&
        summary.Status == (completion is null ? AccessReviewCampaign.Active : AccessReviewCampaign.Completed) &&
        summary.ItemCount == launch.Items.Count && summary.SnapshotId == launch.SnapshotId &&
        summary.LaunchedAt == launch.LaunchedAt &&
        summary.FinalSnapshotId == completion?.SnapshotId &&
        summary.CompletedAt == completion?.CompletedAt;

    static bool ValidSummary(AccessReviewCampaignSummaryView? summary, Uuid tenantId) =>
        summary is not null && summary.TenantId == tenantId && summary.CampaignId != Uuid.Empty &&
        ValidText(summary.Name, 200) && summary.Deadline != default &&
        summary.Status is AccessReviewCampaign.Active or AccessReviewCampaign.Completed &&
        summary.ItemCount > 0 && summary.SnapshotId != Uuid.Empty && summary.LaunchedAt != default &&
        (summary.Status == AccessReviewCampaign.Active
            ? summary.FinalSnapshotId is null && summary.CompletedAt is null
            : summary.FinalSnapshotId is { } finalId && finalId != Uuid.Empty &&
              summary.CompletedAt is { } completedAt && completedAt != default);

    static bool RequiresRemediation(CampaignItemHistory item) =>
        AccessReviewVocabulary.RequiresRemediation(item.Decisions.LastOrDefault()?.Decision);

    static string RemediationStatus(CampaignItemHistory item, DateTimeOffset at)
    {
        if (!RequiresRemediation(item))
            return "not_required";
        if (item.Verification is not null)
            return "verified";
        if (item.Exception is { } exception &&
            (exception.ExpiresAt is null || at < exception.ExpiresAt))
            return "excepted";
        return item.Changes.Count > 0 ? "provider_changed" : "pending";
    }

    static bool ValidMemberActor(ActorReference? actor) => actor is { Kind: "member" } &&
        Uuid.TryParse(actor.Id, null, out var id) && id != Uuid.Empty && ValidText(actor.Display, 200);

    static bool ValidMemberActor(ActorReference? actor, Uuid memberId) =>
        memberId != Uuid.Empty && ValidMemberActor(actor) && actor!.Id == memberId.ToString();

    static Uuid ActorMemberId(ActorReference actor) =>
        Uuid.TryParse(actor.Id, null, out var id) ? id : Uuid.Empty;

    static bool ValidText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && value == value.Trim();

    static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId, ProjectionCheckpoint checkpoint,
        CancellationToken ct)
    {
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(
                tenantId.ToString(), AccessReviewCampaign.Area), checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    static string Digest(AccessReviewCampaignSourceContent content)
    {
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.AccessReviewCampaignSourceContent);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var domain = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[domain.Length + bytes.Length];
        domain.CopyTo(material, 0);
        bytes.CopyTo(material, domain.Length);
        return Convert.ToHexStringLower(SHA256.HashData(material));
    }

    static Result<AccessReviewCampaignHistory> InvalidHistory() =>
        Result<AccessReviewCampaignHistory>.Failure(new RequestError(RequestErrorKind.Conflict,
            "An access review campaign stream is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The access review campaign directory is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "An access review campaign source or immutable snapshot is invalid."));

    static Result<TypeIBaselineSourceContribution> BehindSource() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The access review campaign directory changed or has not reached its source. Retry the query.",
            isTransient: true));

    sealed class CampaignItemHistory(AccessReviewItemView item,
        Uuid? remediationOwnerMemberId)
    {
        public AccessReviewItemView Item { get; } = item;
        public List<AccessDecisionView> Decisions { get; } = [];
        public List<AccessRemediationChangeView> Changes { get; } = [];
        public List<AccessReviewResponsibilityReassignmentView> Reassignments { get; } = [];
        public Dictionary<Uuid, PendingDecision> Pending { get; } = [];
        public Uuid CurrentReviewerMemberId { get; set; } = item.ReviewerMemberId;
        public Uuid? CurrentRemediationOwnerMemberId { get; set; } = remediationOwnerMemberId;
        public AccessRemediationVerificationView? Verification { get; set; }
        public AccessRemediationExceptionView? Exception { get; set; }
    }

    sealed record PendingDecision(AccessDecisionView Decision, long Revision);

    const string ProvisionalKind = "provisional";

    sealed record AsOfEvent(DateTimeOffset At, EventStreamAddress Stream, AccessReviewCampaignSourceEvent Event);

    sealed record AccessReviewCampaignHistory(AccessReviewLaunch Launch,
        AccessReviewCampaignCompletionView? Completion, AccessReviewCampaignSnapshot Snapshot,
        long AsOfRevision, List<AccessReviewCampaignSourceEvent> AsOfEvents);
}
