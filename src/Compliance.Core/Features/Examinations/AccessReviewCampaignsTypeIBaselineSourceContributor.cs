using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures each routed access-review campaign's history on the Type I date.</summary>
public sealed class AccessReviewCampaignsTypeIBaselineSourceContributor(
    IAccessReviewCampaignDirectoryReader directory, IAggregateReader reader,
    IDomainEventReader events) : ITypeIBaselineSourceContributor
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
                !await VerifyCompletionSnapshotAsync(engagement.TenantId, history.Value.Campaign,
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
        var seenEventIds = new HashSet<Uuid>();
        var usedDecisionIds = new HashSet<Uuid>();
        var usedReassignmentIds = new HashSet<Uuid>();
        var asOfEvents = new List<AccessReviewCampaignSourceEvent>();
        var states = new Dictionary<Uuid, CampaignItemHistory>();
        AccessReviewCampaignLaunched? launch = null;
        AccessReviewCampaignCompletionView? completion = null;
        ulong expectedVersion = 1;
        long expectedRevision = 1;
        long asOfRevision = 0;
        DateTimeOffset? priorRecordedAt = null;
        DateTimeOffset? priorOccurredAt = null;

        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            var domainEvent = record.Event;
            var metadata = domainEvent?.Metadata;
            if (record.Stream != stream || domainEvent is null || metadata is null ||
                metadata.EventId == Uuid.Empty || !seenEventIds.Add(metadata.EventId) ||
                metadata.AggregateId != campaignId || metadata.AggregateVersion != expectedVersion ||
                metadata.OccurredOn == default ||
                priorRecordedAt is { } earlierRecord && metadata.OccurredOn < earlierRecord ||
                completion is not null || !TryEventTime(domainEvent, out var occurredAt) ||
                priorOccurredAt is { } earlierEvent && occurredAt < earlierEvent)
                return InvalidHistory();

            if (launch is null)
            {
                if (domainEvent is not AccessReviewCampaignLaunched launched ||
                    !ValidLaunch(launched, tenantId, campaignId))
                    return InvalidHistory();
                launch = launched;
                foreach (var item in launched.Items)
                    states.Add(item.ItemId, new CampaignItemHistory(item,
                        launched.RemediationOwnerMemberId));
            }
            else
            {
                if (!Apply(domainEvent, tenantId, campaignId, expectedRevision, launch, states,
                        usedDecisionIds, usedReassignmentIds, out completion))
                    return InvalidHistory();
            }

            if (occurredAt <= through && metadata.OccurredOn <= through)
            {
                asOfRevision = expectedRevision;
                asOfEvents.Add(SerializeEvent(domainEvent, metadata));
            }
            priorRecordedAt = metadata.OccurredOn;
            priorOccurredAt = occurredAt;
            expectedRevision++;
            expectedVersion++;
        }

        if (launch is null || !MatchesSummary(summary, launch, completion))
            return InvalidHistory();
        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenantId, campaignId), ct)
            .ConfigureAwait(false);
        if (!CurrentStateMatches(campaign, launch, completion, expectedRevision - 1, states))
            return InvalidHistory();

        return Result<AccessReviewCampaignHistory>.Success(new AccessReviewCampaignHistory(
            launch, completion, campaign, asOfRevision, asOfEvents));
    }

    static bool Apply(DomainEvent domainEvent, Uuid tenantId, Uuid campaignId,
        long expectedRevision, AccessReviewCampaignLaunched launch,
        IReadOnlyDictionary<Uuid, CampaignItemHistory> states, HashSet<Uuid> usedDecisionIds,
        HashSet<Uuid> usedReassignmentIds,
        out AccessReviewCampaignCompletionView? completion)
    {
        completion = null;
        switch (domainEvent)
        {
            case AccessDecisionsRecorded decisions:
                if (decisions.TenantId != tenantId || decisions.CampaignId != campaignId ||
                    decisions.Revision != expectedRevision ||
                    !ValidDecisions(decisions, launch, states, usedDecisionIds))
                    return false;
                foreach (var decision in decisions.Decisions)
                    states[decision.ItemId].Decisions.Add(decision);
                return true;

            case AccessRemediationChangeRecorded changed:
                if (changed.TenantId != tenantId || changed.CampaignId != campaignId ||
                    changed.Revision != expectedRevision || changed.Change is not { } change ||
                    !states.TryGetValue(changed.ItemId, out var changedItem) ||
                    !RequiresRemediation(changedItem) || change.ChangeId == Uuid.Empty ||
                    !ValidText(change.Reference, 500) || !ValidText(change.Description, 4000) ||
                    change.ChangedAt == default || change.RecordedAt == default ||
                    change.ChangedAt > change.RecordedAt ||
                    !ValidMemberActor(change.RecordedBy) ||
                    launch.ProgramId is not null &&
                    changedItem.CurrentRemediationOwnerMemberId != ActorMemberId(change.RecordedBy) ||
                    changedItem.Changes.Any(existing => existing.ChangeId == change.ChangeId))
                    return false;
                changedItem.Changes.Add(change);
                return true;

            case AccessRemediationVerified verified:
                if (verified.TenantId != tenantId || verified.CampaignId != campaignId ||
                    verified.Revision != expectedRevision ||
                    verified.Verification is not { } verification ||
                    !states.TryGetValue(verified.ItemId, out var verifiedItem) ||
                    !RequiresRemediation(verifiedItem) ||
                    !ValidVerification(verification, verifiedItem))
                    return false;
                verifiedItem.Verification = verification;
                return true;

            case AccessRemediationExceptionRecorded excepted:
                if (excepted.TenantId != tenantId || excepted.CampaignId != campaignId ||
                    excepted.Revision != expectedRevision ||
                    excepted.Exception is not { } exception ||
                    !states.TryGetValue(excepted.ItemId, out var exceptedItem) ||
                    !RequiresRemediation(exceptedItem) ||
                    !ValidException(exception, exceptedItem))
                    return false;
                exceptedItem.Exception = exception;
                return true;

            case AccessReviewResponsibilityReassigned reassigned:
                if (reassigned.TenantId != tenantId || reassigned.CampaignId != campaignId ||
                    reassigned.Revision != expectedRevision ||
                    reassigned.Reassignment is not { } reassignment ||
                    !states.TryGetValue(reassignment.ItemId, out var reassignedItem) ||
                    !ValidReassignment(reassignment, launch, reassignedItem,
                        usedReassignmentIds))
                    return false;
                reassignedItem.Reassignments.Add(reassignment);
                if (reassignment.Responsibility == AccessReviewResponsibilityKind.Reviewer)
                    reassignedItem.CurrentReviewerMemberId = reassignment.AssignedMemberId;
                else
                    reassignedItem.CurrentRemediationOwnerMemberId = reassignment.AssignedMemberId;
                return true;

            case AccessReviewCampaignCompleted completed:
                if (completed.TenantId != tenantId || completed.CampaignId != campaignId ||
                    completed.Revision != expectedRevision ||
                    !ValidCompletion(completed.Completion, launch, states))
                    return false;
                completion = completed.Completion;
                return true;

            default:
                return false;
        }
    }

    static bool ValidLaunch(AccessReviewCampaignLaunched launch, Uuid tenantId, Uuid campaignId)
    {
        if (launch.TenantId != tenantId || launch.CampaignId != campaignId ||
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

    static bool ValidDecisions(AccessDecisionsRecorded ev, AccessReviewCampaignLaunched launch,
        IReadOnlyDictionary<Uuid, CampaignItemHistory> states, HashSet<Uuid> usedDecisionIds)
    {
        var decisions = ev.Decisions;
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
        AccessReviewCampaignLaunched launch, CampaignItemHistory item,
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
        AccessReviewCampaignLaunched launch,
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

    static bool CurrentStateMatches(AccessReviewCampaign campaign,
        AccessReviewCampaignLaunched launch, AccessReviewCampaignCompletionView? completion,
        long revision, IReadOnlyDictionary<Uuid, CampaignItemHistory> states)
    {
        var json = ComplianceCoreJsonContext.Default;
        if (campaign.Id != launch.CampaignId || campaign.Revision != revision ||
            !SameJson(campaign.Launched, launch, json.AccessReviewCampaignLaunched) ||
            !SameJson(campaign.Completion, completion, json.AccessReviewCampaignCompletionView))
            return false;
        var view = campaign.ToView(DateTimeOffset.MaxValue);
        if (view.TenantId == Uuid.Empty || view.CampaignId != launch.CampaignId ||
            view.Revision != revision || view.Status != (completion is null
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
        AccessReviewCampaignLaunched launch, CancellationToken ct)
    {
        var content = await PopulationSnapshotContent.ReadAsync(reader, tenantId, launch.SnapshotId,
            AccessReviewCampaignContent.LaunchKind, ct).ConfigureAwait(false);
        if (!content.IsSuccess)
            return false;
        var snapshot = content.Value.Snapshot;
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
        AccessReviewCampaign campaign, AccessReviewCampaignLaunched launch,
        AccessReviewCampaignCompletionView completion, CancellationToken ct)
    {
        var content = await PopulationSnapshotContent.ReadAsync(reader, tenantId,
            completion.SnapshotId, AccessReviewCampaignContent.ResultKind, ct).ConfigureAwait(false);
        if (!content.IsSuccess)
            return false;
        var snapshot = content.Value.Snapshot;
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
        AccessReviewCampaignLaunched launch, AccessReviewCampaignCompletionView? completion) =>
        summary.TenantId == launch.TenantId && summary.CampaignId == launch.CampaignId &&
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

    static bool TryEventTime(DomainEvent domainEvent, out DateTimeOffset occurredAt)
    {
        switch (domainEvent)
        {
            case AccessReviewCampaignLaunched launched:
                occurredAt = launched.LaunchedAt;
                return occurredAt != default;
            case AccessDecisionsRecorded { Decisions: { Count: > 0 } decisions }:
                if (decisions[0] is not { } first || first.DecidedAt == default ||
                    decisions.Any(decision => decision is null || decision.DecidedAt != first.DecidedAt))
                {
                    occurredAt = default;
                    return false;
                }
                occurredAt = first.DecidedAt;
                return true;
            case AccessRemediationChangeRecorded { Change: { } change }:
                occurredAt = change.RecordedAt;
                return occurredAt != default;
            case AccessRemediationVerified { Verification: { } verification }:
                occurredAt = verification.VerifiedAt;
                return occurredAt != default;
            case AccessRemediationExceptionRecorded { Exception: { } exception }:
                occurredAt = exception.ApprovedAt;
                return occurredAt != default;
            case AccessReviewResponsibilityReassigned { Reassignment: { } reassignment }:
                occurredAt = reassignment.ReassignedAt;
                return occurredAt != default;
            case AccessReviewCampaignCompleted { Completion: { } completion }:
                occurredAt = completion.CompletedAt;
                return occurredAt != default;
            default:
                occurredAt = default;
                return false;
        }
    }

    static AccessReviewCampaignSourceEvent SerializeEvent(DomainEvent domainEvent,
        DomainEventMetadata metadata)
    {
        var json = ComplianceCoreJsonContext.Default;
        return domainEvent switch
        {
            AccessReviewCampaignLaunched launched => new AccessReviewCampaignSourceEvent(
                metadata.EventId, metadata.AggregateVersion, metadata.OccurredOn,
                "campaign_launched", JsonSerializer.SerializeToElement(launched,
                    json.AccessReviewCampaignLaunched)),
            AccessDecisionsRecorded decisions => new AccessReviewCampaignSourceEvent(
                metadata.EventId, metadata.AggregateVersion, metadata.OccurredOn,
                "decisions_recorded", JsonSerializer.SerializeToElement(decisions,
                    json.AccessDecisionsRecorded)),
            AccessRemediationChangeRecorded change => new AccessReviewCampaignSourceEvent(
                metadata.EventId, metadata.AggregateVersion, metadata.OccurredOn,
                "remediation_change_recorded", JsonSerializer.SerializeToElement(change,
                    json.AccessRemediationChangeRecorded)),
            AccessRemediationVerified verified => new AccessReviewCampaignSourceEvent(
                metadata.EventId, metadata.AggregateVersion, metadata.OccurredOn,
                "remediation_verified", JsonSerializer.SerializeToElement(verified,
                    json.AccessRemediationVerified)),
            AccessRemediationExceptionRecorded exception => new AccessReviewCampaignSourceEvent(
                metadata.EventId, metadata.AggregateVersion, metadata.OccurredOn,
                "remediation_exception_recorded", JsonSerializer.SerializeToElement(exception,
                    json.AccessRemediationExceptionRecorded)),
            AccessReviewResponsibilityReassigned reassigned => new AccessReviewCampaignSourceEvent(
                metadata.EventId, metadata.AggregateVersion, metadata.OccurredOn,
                "responsibility_reassigned", JsonSerializer.SerializeToElement(reassigned,
                    json.AccessReviewResponsibilityReassigned)),
            AccessReviewCampaignCompleted completed => new AccessReviewCampaignSourceEvent(
                metadata.EventId, metadata.AggregateVersion, metadata.OccurredOn,
                "campaign_completed", JsonSerializer.SerializeToElement(completed,
                    json.AccessReviewCampaignCompleted)),
            _ => throw new JsonException("The campaign stream contains an unknown event."),
        };
    }

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
        public Uuid CurrentReviewerMemberId { get; set; } = item.ReviewerMemberId;
        public Uuid? CurrentRemediationOwnerMemberId { get; set; } = remediationOwnerMemberId;
        public AccessRemediationVerificationView? Verification { get; set; }
        public AccessRemediationExceptionView? Exception { get; set; }
    }

    sealed record AccessReviewCampaignHistory(AccessReviewCampaignLaunched Launch,
        AccessReviewCampaignCompletionView? Completion, AccessReviewCampaign Campaign,
        long AsOfRevision, List<AccessReviewCampaignSourceEvent> AsOfEvents);
}
