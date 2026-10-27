using Bdgrz.Compliance.Features.SourceFacts;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Shares exact commitment-version capture rules across baseline source families.</summary>
public sealed class CommitmentTypeIBaselineSourceReader(
    CommitmentReader commitments, IDomainEventReader events, IImmutableContentStore content,
    CommitmentVersionReadConsistency versions, CommitmentDraftListReadConsistency consistency,
    IBoundaryDirectoryReader boundaries, ProgramSetupWorkReadConsistency boundaryConsistency)
    : IProviderCsocBaselineReader
{
    static readonly HashSet<string> AllKinds = new(StringComparer.Ordinal)
    {
        "service_commitment", "system_requirement", "user_entity_responsibility",
        "subservice_responsibility",
    };

    static readonly IReadOnlySet<string> ProviderCsocKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "service_commitment", "user_entity_responsibility", "subservice_responsibility",
    };

    public async ValueTask<Result<IReadOnlySet<Uuid>>> ReadEffectiveProviderCsocsAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context?.PreviouslyCapturedSources is null)
            return Result<IReadOnlySet<Uuid>>.Failure(new RequestError(RequestErrorKind.Validation,
                "A provider CSOC read requires its engagement and source context."));

        var priorBoundaries = context.PreviouslyCapturedSources.Where(static source =>
            source.Kind == "approved_boundary").ToArray();
        if (priorBoundaries.Length != 1 || priorBoundaries[0].RecordId != engagement.BoundaryId)
            return Result<IReadOnlySet<Uuid>>.Success(new HashSet<Uuid>());

        var read = await CommitmentSourceFailures.ClassifyAsync(() => ReadCoreAsync("commitments", ProviderCsocKinds, engagement, context,
            includeProviderCsocReferences: true, ct)).ConfigureAwait(false);
        if (!read.IsSuccess)
            return Result<IReadOnlySet<Uuid>>.Failure(read.Error);

        var priorSources = context.PreviouslyCapturedSources.Where(IsCapturedCommitmentSource)
            .OrderBy(static source => source.Kind, StringComparer.Ordinal)
            .ThenBy(static source => source.RecordId.ToString(), StringComparer.Ordinal).ToArray();
        var currentSources = read.Value.Sources.Where(IsCapturedCommitmentSource)
            .OrderBy(static source => source.Kind, StringComparer.Ordinal)
            .ThenBy(static source => source.RecordId.ToString(), StringComparer.Ordinal).ToArray();
        if (!SameSourceReferences(priorSources, currentSources))
            return Result<IReadOnlySet<Uuid>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The commitment sources changed while provider scope was being read. Retry the preview.",
                isTransient: true));

        IReadOnlySet<Uuid> providerIds = read.Value.Sources.Where(static source =>
                source.Kind == "provider_csocs").Select(static source => source.RecordId).ToHashSet();
        return Result<IReadOnlySet<Uuid>>.Success(providerIds);
    }

    static bool IsCapturedCommitmentSource(TypeIBaselineSourceReference source) =>
        source.Kind is "commitment_version" or "commitment_retirement";

    static bool SameSourceReferences(TypeIBaselineSourceReference[] left,
        TypeIBaselineSourceReference[] right) => left.Length == right.Length &&
        left.Zip(right).All(static pair => pair.First.Kind == pair.Second.Kind &&
            pair.First.RecordId == pair.Second.RecordId && pair.First.VersionId == pair.Second.VersionId &&
            pair.First.Revision == pair.Second.Revision &&
            pair.First.ContentSha256 == pair.Second.ContentSha256);

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        string family, IReadOnlySet<string> includedKinds, ExaminationEngagementView engagement,
        CancellationToken ct) => ReadAsync(family, includedKinds, engagement,
        new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        string family, IReadOnlySet<string> includedKinds, ExaminationEngagementView engagement,
        TypeIBaselineSourceReadContext context, CancellationToken ct) =>
        CommitmentSourceFailures.ClassifyAsync(() => ReadCoreAsync(family, includedKinds, engagement, context,
            includeProviderCsocReferences: false, ct));

    async ValueTask<Result<TypeIBaselineSourceContribution>> ReadCoreAsync(
        string family, IReadOnlySet<string> includedKinds, ExaminationEngagementView engagement,
        TypeIBaselineSourceReadContext context, bool includeProviderCsocReferences,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A commitment baseline read requires its engagement and source context."));

        var priorBoundaries = context.PreviouslyCapturedSources.Where(static source =>
            source.Kind == "approved_boundary").ToArray();
        if (priorBoundaries.Length != 1 || priorBoundaries[0].RecordId != engagement.BoundaryId)
            return Success(family, [], ["Commitments were not read from the engagement's exact approved boundary."], []);

        var boundaryFence = await boundaryConsistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!boundaryFence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(boundaryFence.Error);
        var commitmentFence = await consistency.CaptureFenceAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!commitmentFence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(commitmentFence.Error);

        var boundary = await boundaries.GetAsync(engagement.TenantId, engagement.BoundaryId, ct)
            .ConfigureAwait(false);
        var boundaryVersion = await boundaries.GetEffectiveVersionAsync(engagement.TenantId,
            engagement.BoundaryId, engagement.PeriodStart, ct).ConfigureAwait(false);
        if (boundary is null || boundary.TenantId != engagement.TenantId ||
            boundary.ProgramId != engagement.ProgramId || boundary.BoundaryId != engagement.BoundaryId ||
            boundaryVersion is null || boundaryVersion.TenantId != engagement.TenantId ||
            boundaryVersion.ProgramId != engagement.ProgramId ||
            boundaryVersion.BoundaryId != engagement.BoundaryId ||
            boundaryVersion.VersionId != priorBoundaries[0].VersionId ||
            boundaryVersion.Revision != priorBoundaries[0].Revision ||
            boundaryVersion.Status != "approved" ||
            boundaryVersion.EffectiveFrom is not { } effectiveFrom ||
            effectiveFrom > engagement.PeriodStart)
            return BoundaryChanged();

        string boundaryDigest;
        try
        {
            boundaryDigest = SnapshotContentIdentity.ApprovedBoundaryVersion(boundaryVersion);
        }
        catch (SnapshotContentException)
        {
            return Success(family, [], ["The commitment scope has invalid approved boundary content."], []);
        }
        if (!StringComparer.Ordinal.Equals(boundaryDigest, priorBoundaries[0].ContentSha256))
            return BoundaryChanged();

        var commitmentIds = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            StringComparer.Ordinal.Compare(left.ToString(), right.ToString())));
        var serviceIds = boundaryVersion.Content.Entries.Where(static entry =>
                entry.Kind == "inclusion" && entry.SubjectType == "service" && !entry.Unresolved &&
                entry.GovernedRecordId is not null)
            .Select(static entry => entry.GovernedRecordId!.Value).ToHashSet();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var entry in boundaryVersion.Content.Entries.Where(static entry =>
                     entry.Kind == "inclusion" && entry.SubjectType == "commitment"))
        {
            if (entry.Unresolved || entry.GovernedRecordId is not { } draftId || draftId == Uuid.Empty)
            {
                blockers.Add("An included commitment in the approved boundary cannot be resolved.");
                scopeDifferences.Add($"Boundary scope entry {entry.EntryId} does not identify a resolved commitment.");
                continue;
            }
            commitmentIds.Add(draftId);
        }

        var sources = new List<TypeIBaselineSourceReference>();
        var providerCsocs = new Dictionary<Uuid, List<TypeIBaselineSourceReference>>();
        foreach (var draftId in commitmentIds)
        {
            var draft = await commitments.ReadAsync(engagement.TenantId, engagement.ProgramId, draftId, ct).ConfigureAwait(false);
            if (!draft.IsCreated)
            {
                blockers.Add("A commitment included by the approved boundary has no source history.");
                continue;
            }
            if (draft.ProgramId != engagement.ProgramId || draft.Id != draftId ||
                draft.Kind is null || !AllKinds.Contains(draft.Kind))
                return SourceConflict();
            if (!includedKinds.Contains(draft.Kind))
                continue;

            if (!serviceIds.Contains(draft.ServiceId))
            {
                blockers.Add("An included commitment's service is not included by the approved boundary.");
                scopeDifferences.Add($"Commitment {draft.Identifier} has a service outside the approved boundary.");
            }

            var effectiveVersion = draft.EffectiveVersionOn(engagement.PeriodStart);
            if (effectiveVersion is null)
            {
                blockers.Add("A commitment included by the approved boundary has no approved version effective on the Type I date.");
                continue;
            }

            var viewResult = await versions.GetAsync(engagement.TenantId, engagement.ProgramId,
                draftId, effectiveVersion.Value, ct).ConfigureAwait(false);
            if (!viewResult.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(viewResult.Error);
            var view = viewResult.Value;
            var receiptStream = context.RecordedThrough is null ? null :
                await context.RequireReceipts(engagement.TenantId).ReadAsync(new EventStreamAddress(
                    engagement.TenantId.ToString(), CommitmentDraft.Area, draftId.ToString()), ct).ConfigureAwait(false);
            if (receiptStream is not null && !receiptStream.Includes(fact => fact is CommitmentApproved approved &&
                    approved.ChangeCount > 0 && approved.Version == view.Version && approved.Revision == view.Revision))
                return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "An effective commitment changed after the frozen close receipt cutoff.", true));
            var history = await ReadHistoryAsync(engagement.TenantId, engagement.ProgramId,
                draftId, draft.Owner.NativeSequence, ct).ConfigureAwait(false);
            if (!history.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
            if (!MatchesDraft(draft, history.Value) ||
                !ValidateVersion(view, effectiveVersion.Value, engagement, draft, history.Value))
                return SourceConflict();

            var stem = family == "commitments" ? "commitment" : "system_requirement";
            TypeIBaselineSourceReference sourceReference;
            try
            {
                sourceReference = new TypeIBaselineSourceReference(stem + "_version", draftId,
                    Uuid.CreateVersion5(draftId, $"{stem}_version_v1:{view.Version}"),
                    view.Revision, VersionDigest(stem, view));
                sources.Add(sourceReference);
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException)
            {
                blockers.Add("A commitment has invalid canonical source content.");
                scopeDifferences.Add($"Commitment {draft.Identifier} has invalid canonical source content.");
                continue;
            }

            if (view.Approval is null)
                blockers.Add($"Commitment {draft.Identifier} has no separate management approval for its effective version.");
            if (view.SourceResolution != "verified" || string.IsNullOrWhiteSpace(view.SourceEvidence))
            {
                blockers.Add($"Commitment {draft.Identifier} has no verified source evidence for its effective version.");
                scopeDifferences.Add($"Commitment {draft.Identifier} has an unverified source reference.");
            }
            if (view.Interpretation != "supported")
            {
                blockers.Add($"Commitment {draft.Identifier} has an unsupported interpretation.");
                scopeDifferences.Add($"Commitment {draft.Identifier} has an unsupported interpretation.");
            }
            if (view.Applicability == "not_applicable")
                scopeDifferences.Add($"Commitment {draft.Identifier} was marked not applicable on the Type I date.");
            if (view.Kind == "subservice_responsibility" &&
                (view.ProviderId is not { } providerId || providerId == Uuid.Empty))
            {
                blockers.Add($"Subservice responsibility {draft.Identifier} has no provider reference.");
                scopeDifferences.Add($"Subservice responsibility {draft.Identifier} has no provider reference.");
            }

            var retirement = history.Value.Retirement;
            if (family == "commitments" && view.Kind == "subservice_responsibility" &&
                view.ProviderId is { } csocProviderId && csocProviderId != Uuid.Empty &&
                view.Applicability == "applicable" && view.Interpretation == "supported" &&
                (retirement is null || retirement.EffectiveOn > engagement.PeriodStart))
            {
                if (!providerCsocs.TryGetValue(csocProviderId, out var references))
                    providerCsocs.Add(csocProviderId, references = []);
                references.Add(sourceReference);
            }
            if (retirement is null)
                continue;
            var effectiveThrough = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
                DateTimeKind.Utc));
            var through = context.RecordedThrough ?? effectiveThrough;
            var retirementRecorded = receiptStream is null ? retirement.OccurredAt <= through :
                receiptStream.Includes(fact => fact is CommitmentRetired retired && retired.ChangeCount > 0 &&
                    retired.Revision == retirement.Revision);
            if (receiptStream is not null && !retirementRecorded && retirement.EffectiveOn <= engagement.PeriodStart)
                return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "A commitment retirement changed after the frozen close receipt cutoff.", true));
            if (retirementRecorded || retirement.EffectiveOn <= engagement.PeriodStart)
            {
                var retirementKind = stem + "_retirement";
                try
                {
                    sources.Add(new TypeIBaselineSourceReference(retirementKind, draftId,
                        Uuid.CreateVersion5(draftId,
                            $"{retirementKind}_v1:{retirement.Revision}"), retirement.Revision,
                        RetirementDigest(stem, retirement)));
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException or
                                                   InvalidOperationException or NotSupportedException)
                {
                    blockers.Add($"Commitment {draft.Identifier} has invalid canonical retirement content.");
                    scopeDifferences.Add($"Commitment {draft.Identifier} has invalid canonical retirement content.");
                }
                if (retirement.EffectiveOn > engagement.PeriodStart)
                    scopeDifferences.Add($"Commitment {draft.Identifier} has a retirement scheduled after the Type I date.");
                else if (!retirementRecorded)
                    scopeDifferences.Add($"Commitment {draft.Identifier} was retired retroactively after the Type I date.");
                else
                    scopeDifferences.Add($"Commitment {draft.Identifier} was retired on or before the Type I date.");
            }
        }

        if (includeProviderCsocReferences)
        {
            foreach (var (providerId, references) in providerCsocs.OrderBy(static item =>
                         item.Key.ToString(), StringComparer.Ordinal))
            {
                var digest = TypeIBaselineContentIdentity.Coverage("provider_csocs", references)
                    .ContentSha256;
                sources.Add(new TypeIBaselineSourceReference("provider_csocs", providerId,
                    Uuid.CreateVersion5(providerId, "provider_csocs_v1:" +
                        engagement.PeriodStart.ToString("yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture)), 1, digest));
            }
        }

        var commitmentsConfirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(
            engagement.TenantId, commitmentFence.Value, ct).ConfigureAwait(false);
        if (!commitmentsConfirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(commitmentsConfirmed.Error);
        var boundaryConfirmed = await boundaryConsistency.ConfirmUnchangedAndCaughtUpAsync(
            engagement.TenantId, boundaryFence.Value, ct).ConfigureAwait(false);
        if (!boundaryConfirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(boundaryConfirmed.Error);

        return Success(family, sources, blockers, scopeDifferences);
    }

    async ValueTask<Result<CommitmentHistory>> ReadHistoryAsync(Uuid tenantId, Uuid programId,
        Uuid draftId, long nativeSequence, CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), CommitmentDraft.Area, draftId.ToString());
        var eventsInStream = new List<ICommitmentMutationContent>();
        var sequence = 0L;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Stream != stream || record.Event is not ICommitmentFact fact ||
                fact.NativeSequence != sequence + 1 || fact.NativeSequence > nativeSequence)
                return InvalidHistory();
            var mutation = await CommitmentFactContent.ReadChangeAsync(content, fact, ct).ConfigureAwait(false);
            foreach (var change in mutation.Changes)
            {
                if (!IsKnownEvent(change))
                    return InvalidHistory();
                eventsInStream.Add(change);
            }
            sequence = fact.NativeSequence;
            if (sequence == nativeSequence)
                break;
        }
        if (sequence != nativeSequence)
            throw new ImmutableContentUnavailableException("The exact commitment native source cut is not visible yet.");

        var created = eventsInStream.OfType<CommitmentDraftCreatedContent>().ToArray();
        if (created.Length != 1 || eventsInStream[0] is not CommitmentDraftCreatedContent ||
            !IsValid(created[0], tenantId, programId, draftId))
            return InvalidHistory();
        var revision = 1L;
        var effectiveVersions = new List<(long Version, long Revision, DateOnly EffectiveFrom)>();
        var acceptedReviews = new Dictionary<long, Uuid>();
        var effectiveRevisions = new HashSet<long>();
        var decisionIds = new HashSet<Uuid>();
        CommitmentRetiredContent? retirement = null;
        foreach (var item in eventsInStream)
        {
            if (retirement is not null)
                return InvalidHistory();

            if (item is CommitmentDraftRevisedContent change)
            {
                if (!IsValid(change, tenantId, programId, draftId, revision + 1))
                    return InvalidHistory();
                revision++;
                acceptedReviews.Clear();
            }
            else if (item is CommitmentReviewedContent review)
            {
                if (!IsValid(review, tenantId, programId, draftId, revision) ||
                    review.Revision != revision || !decisionIds.Add(review.DecisionId) ||
                    effectiveRevisions.Contains(review.Revision))
                    return InvalidHistory();
                if (review.Version is { } version && review.EffectiveFrom is { } effectiveFrom)
                {
                    if (version != effectiveVersions.Count + 1 ||
                        effectiveVersions.Count > 0 && effectiveFrom <= effectiveVersions[^1].EffectiveFrom)
                        return InvalidHistory();
                    effectiveVersions.Add((version, review.Revision, effectiveFrom));
                    effectiveRevisions.Add(review.Revision);
                    acceptedReviews.Remove(review.Revision);
                }
                else if (review.Outcome == "accept")
                    acceptedReviews[review.Revision] = review.DecisionId;
                else
                    acceptedReviews.Remove(review.Revision);
            }
            else if (item is CommitmentApprovedContent approval)
            {
                if (!IsValid(approval, tenantId, programId, draftId, revision) ||
                    approval.Revision != revision || !decisionIds.Add(approval.DecisionId) ||
                    effectiveRevisions.Contains(approval.Revision) ||
                    !acceptedReviews.TryGetValue(approval.Revision, out var acceptedReviewId) ||
                    acceptedReviewId != approval.AcceptedReviewDecisionId ||
                    approval.Version != effectiveVersions.Count + 1 ||
                    effectiveVersions.Count > 0 && approval.EffectiveFrom <= effectiveVersions[^1].EffectiveFrom)
                    return InvalidHistory();
                acceptedReviews.Remove(approval.Revision);
                effectiveRevisions.Add(approval.Revision);
                effectiveVersions.Add((approval.Version, approval.Revision, approval.EffectiveFrom));
            }
            else if (item is CommitmentRetiredContent retired)
            {
                if (!IsValid(retired, tenantId, programId, draftId, revision,
                        effectiveVersions.Count == 0 ? null : effectiveVersions[^1].EffectiveFrom))
                    return InvalidHistory();
                retirement = retired;
            }
        }

        var revised = eventsInStream.OfType<CommitmentDraftRevisedContent>().ToArray();
        return Result<CommitmentHistory>.Success(new CommitmentHistory(created[0], revised,
            eventsInStream.OfType<CommitmentReviewedContent>().ToArray(),
            eventsInStream.OfType<CommitmentApprovedContent>().ToArray(), retirement,
            effectiveVersions.Count, revision));
    }

    static bool ValidateVersion(CommitmentVersionView view, long expectedVersion,
        ExaminationEngagementView engagement, CommitmentReadSnapshot draft, CommitmentHistory history)
    {
        if (view is null || view.Decision is null ||
            view.TenantId != engagement.TenantId || view.ProgramId != engagement.ProgramId ||
            view.DraftId != draft.Id || view.ServiceId != draft.ServiceId ||
            view.Kind != draft.Kind || view.Identifier != draft.Identifier ||
            view.ProviderId != draft.ProviderId || view.Version != expectedVersion ||
            view.Revision < 1 || view.Revision > history.DraftRevision ||
            view.EffectiveFrom > engagement.PeriodStart ||
            CommitmentDraft.PerformedBy(view.Kind) != view.PerformedBy ||
            CommitmentDraft.IsInternallyPerformed(view.Kind) != view.InternallyPerformed)
            return false;

        var revision = view.Revision == 1
            ? null
            : history.Revisions.SingleOrDefault(item => item.Revision == view.Revision);
        var statement = revision?.Statement ?? history.Created.Statement;
        var context = revision?.Context ?? history.Created.Context;
        var sourceReference = revision?.SourceReference ?? history.Created.SourceReference;
        if (view.Statement != statement || view.Context != context ||
            view.SourceReference != sourceReference ||
            view.Decision.Stage != "review" || view.Decision.Outcome != "accept" ||
            view.Decision.Revision != view.Revision || view.Decision.DecisionId == Uuid.Empty)
            return false;

        var reviewed = history.Reviews.SingleOrDefault(item =>
            item.DecisionId == view.Decision.DecisionId);
        if (reviewed is null || !MatchesReview(view.Decision, reviewed) ||
            view.SourceResolution != (reviewed.SourceVerification ?? "unverified") ||
            view.SourceEvidence != reviewed.SourceEvidence)
            return false;

        var approved = history.Approvals.SingleOrDefault(item =>
            item.Version == expectedVersion);
        var legacyReview = history.Reviews.SingleOrDefault(item => item.Version == expectedVersion);
        if ((approved is null) == (legacyReview is null))
            return false;
        if (approved is null)
            return view.Approval is null && legacyReview!.Revision == view.Revision &&
                   legacyReview.EffectiveFrom == view.EffectiveFrom &&
                   legacyReview.DecisionId == view.Decision.DecisionId;
        return view.Approval is { } approval && approved.Revision == view.Revision &&
               approved.EffectiveFrom == view.EffectiveFrom &&
               approved.AcceptedReviewDecisionId == view.Decision.DecisionId &&
               MatchesApproval(approval, approved);
    }

    static bool MatchesReview(CommitmentDecisionView view, CommitmentReviewedContent source) =>
        view.TenantId == source.TenantId && view.ProgramId == source.ProgramId &&
        view.DraftId == source.DraftId && view.DecisionId == source.DecisionId &&
        view.Revision == source.Revision && view.Outcome == source.Outcome &&
        view.OwnerReference == source.OwnerReference && view.Applicability == source.Applicability &&
        view.Interpretation == source.Interpretation &&
        view.InterpretationNote == source.InterpretationNote && view.Rationale == source.Rationale &&
        view.Version == source.Version && view.EffectiveFrom == source.EffectiveFrom &&
        view.ImpactDigest == source.ImpactDigest && view.ActorMemberId == source.ActorMemberId &&
        view.ActorDisplay == source.ActorDisplay && view.DecidedAt == source.DecidedAt &&
        view.SeparationOfDutiesWaiverId == source.SeparationOfDutiesWaiverId &&
        view.Stage == "review" && view.AcceptedReviewDecisionId is null &&
        view.SourceVerification == source.SourceVerification &&
        view.SourceVerifiedReference == source.SourceVerifiedReference &&
        view.SourceEvidence == source.SourceEvidence && view.Actor == source.Actor;

    static bool MatchesApproval(CommitmentDecisionView view, CommitmentApprovedContent source) =>
        view.TenantId == source.TenantId && view.ProgramId == source.ProgramId &&
        view.DraftId == source.DraftId && view.DecisionId == source.DecisionId &&
        view.Revision == source.Revision && view.Outcome == "approve" &&
        view.OwnerReference is null && view.Applicability is null &&
        view.Interpretation is null && view.InterpretationNote is null &&
        view.Rationale == source.Rationale && view.Version == source.Version &&
        view.EffectiveFrom == source.EffectiveFrom && view.ImpactDigest == source.ImpactDigest &&
        view.ActorMemberId == source.ActorMemberId && view.ActorDisplay == source.ActorDisplay &&
        view.DecidedAt == source.DecidedAt &&
        view.SeparationOfDutiesWaiverId == source.SeparationOfDutiesWaiverId &&
        view.Stage == "approval" &&
        view.AcceptedReviewDecisionId == source.AcceptedReviewDecisionId &&
        view.SourceVerification is null && view.SourceVerifiedReference is null &&
        view.SourceEvidence is null && view.Actor == source.Actor;

    static bool MatchesDraft(CommitmentReadSnapshot draft, CommitmentHistory history) =>
        draft.IsCreated && draft.Id == history.Created.DraftId &&
        draft.ProgramId == history.Created.ProgramId && draft.ServiceId == history.Created.ServiceId &&
        draft.Kind == history.Created.Kind && draft.Identifier == history.Created.Identifier &&
        draft.ProviderId == history.Created.ProviderId && draft.Revision == history.DraftRevision &&
        draft.SourceReference == (history.Revisions.Count == 0
            ? history.Created.SourceReference
            : history.Revisions[history.Revisions.Count - 1].SourceReference) &&
        draft.EffectiveVersionCount == history.VersionCount &&
        draft.IsRetired == (history.Retirement is not null) &&
        draft.RetiredEffectiveOn == history.Retirement?.EffectiveOn &&
        draft.RetirementRationale == history.Retirement?.Rationale;

    static bool IsKnownEvent(ICommitmentMutationContent domainEvent) => domainEvent is CommitmentDraftCreatedContent or
        CommitmentDraftRevisedContent or CommitmentReviewedContent or CommitmentApprovedContent or CommitmentRetiredContent or
        ResponsibilityAssigned or ResponsibilityRevoked;

    static bool IsValid(CommitmentDraftCreatedContent ev, Uuid tenantId, Uuid programId, Uuid draftId) =>
        ev.TenantId == tenantId && ev.ProgramId == programId && ev.DraftId == draftId &&
        ev.CreateRequestId != Uuid.Empty && ev.ServiceId != Uuid.Empty &&
        ev.Kind is not null && AllKinds.Contains(ev.Kind) && ev.Identifier is not null &&
        ev.Identifier == CommitmentDraft.NormalizeIdentifier(ev.Identifier) &&
        ValidText(ev.Statement, 16000) && ev.Context is not null && ev.Context.Length <= 16000 &&
        ValidText(ev.SourceReference, 1024) &&
        (ev.Kind == "subservice_responsibility" ? ev.ProviderId is { } provider && provider != Uuid.Empty : ev.ProviderId is null) &&
        ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.Actor, ev.ChangedAt);

    static bool IsValid(CommitmentDraftRevisedContent ev, Uuid tenantId, Uuid programId, Uuid draftId,
        long revision) => ev.TenantId == tenantId && ev.ProgramId == programId &&
        ev.DraftId == draftId && ev.Revision == revision &&
        ValidText(ev.Statement, 16000) && ev.Context is not null && ev.Context.Length <= 16000 &&
        ValidText(ev.SourceReference, 1024) &&
        ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.Actor, ev.ChangedAt);

    static bool IsValid(CommitmentReviewedContent ev, Uuid tenantId, Uuid programId, Uuid draftId,
        long currentRevision) => ev.TenantId == tenantId && ev.ProgramId == programId &&
        ev.DraftId == draftId && ev.Revision is >= 1 && ev.Revision <= currentRevision &&
        ev.DecisionId != Uuid.Empty && ev.Outcome is "accept" or "request_changes" &&
        ValidText(ev.Rationale, 4000) && ev.DecidedAt != default &&
        ((ev.Version is null && ev.EffectiveFrom is null) ||
         (ev.Version is >= 1 && ev.EffectiveFrom is { } effectiveFrom && effectiveFrom != default)) &&
        (ev.Outcome == "accept"
            ? ValidText(ev.OwnerReference, 200) &&
              ev.Applicability is "applicable" or "not_applicable" &&
              ev.Interpretation is "supported" or "unsupported" &&
              (ev.Version is not null || ev.SourceVerification == "verified" &&
                  ValidText(ev.SourceVerifiedReference, 1024) && ValidText(ev.SourceEvidence, 2000))
            : ev.OwnerReference is null && ev.Applicability is null && ev.Interpretation is null &&
              ev.InterpretationNote is null && ev.SourceVerification is null &&
              ev.SourceVerifiedReference is null && ev.SourceEvidence is null && ev.Version is null) &&
        ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.Actor, ev.DecidedAt);

    static bool IsValid(CommitmentApprovedContent ev, Uuid tenantId, Uuid programId, Uuid draftId,
        long currentRevision) => ev.TenantId == tenantId && ev.ProgramId == programId &&
        ev.DraftId == draftId && ev.Revision is >= 1 && ev.Revision <= currentRevision &&
        ev.DecisionId != Uuid.Empty && ev.AcceptedReviewDecisionId != Uuid.Empty &&
        ev.DecisionId != ev.AcceptedReviewDecisionId &&
        ev.Version >= 1 && ev.EffectiveFrom != default &&
        !string.IsNullOrWhiteSpace(ev.ImpactDigest) && ValidText(ev.Rationale, 4000) &&
        ValidActor(ev.ActorMemberId, ev.ActorDisplay, ev.Actor, ev.DecidedAt);

    static bool IsValid(CommitmentRetiredContent ev, Uuid tenantId, Uuid programId, Uuid draftId,
        long currentRevision, DateOnly? lastEffective) => ev.TenantId == tenantId &&
        ev.ProgramId == programId && ev.SubjectId == draftId && ev.Revision == currentRevision &&
        lastEffective is { } last && ev.EffectiveOn > last && ev.EffectiveOn != default &&
        ValidText(ev.Rationale, 4000) && ev.OccurredAt != default &&
        ev.ActorMemberId is { } actorId && ValidActor(actorId, ev.ActorDisplay, ev.Actor,
            ev.OccurredAt);

    static bool ValidActor(Uuid memberId, string display, ActorReference actor,
        DateTimeOffset changedAt) => memberId != Uuid.Empty && ValidText(display, 200) &&
        changedAt != default && actor == ActorReference.ForMember(memberId, display);

    static bool ValidText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && value == value.Trim();

    static string VersionDigest(string stem, CommitmentVersionView view) =>
        Digest($"bdgrz.snapshot.source.{stem}_version.v1\0", view,
            ComplianceCoreJsonContext.Default.CommitmentVersionView);

    static string RetirementDigest(string stem, CommitmentRetiredContent retirement) =>
        Digest($"bdgrz.snapshot.source.{stem}_retirement.v1\0", retirement,
            ComplianceCoreJsonContext.Default.CommitmentRetiredContent);

    static string Digest<T>(string domain, T content,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var element = JsonSerializer.SerializeToElement(content, typeInfo);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var prefix = Encoding.UTF8.GetBytes(domain);
        var bytes = stream.ToArray();
        var material = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(material, 0);
        bytes.CopyTo(material, prefix.Length);
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }

    static Result<CommitmentHistory> InvalidHistory() =>
        Result<CommitmentHistory>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The commitment source history is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> BoundaryChanged() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The approved boundary changed while commitment scope was being read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The commitment source or projection changed during the baseline read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> Success(string family,
        IReadOnlyList<TypeIBaselineSourceReference> sources, IReadOnlyList<string> blockers,
        IReadOnlyList<string> scopeDifferences) =>
        Result<TypeIBaselineSourceContribution>.Success(new TypeIBaselineSourceContribution(
            family, sources, blockers, scopeDifferences));

    sealed record CommitmentHistory(CommitmentDraftCreatedContent Created,
        IReadOnlyList<CommitmentDraftRevisedContent> Revisions,
        IReadOnlyList<CommitmentReviewedContent> Reviews, IReadOnlyList<CommitmentApprovedContent> Approvals,
        CommitmentRetiredContent? Retirement, long VersionCount, long DraftRevision);
}

public sealed class CommitmentTypeIBaselineSourceContributor(
    CommitmentTypeIBaselineSourceReader reader) : ITypeIBaselineSourceContributor
{
    static readonly IReadOnlySet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "service_commitment", "user_entity_responsibility", "subservice_responsibility",
    };

    public string Family => "commitments";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        reader.ReadAsync(Family, Kinds, engagement, ct);

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct) => reader.ReadAsync(Family, Kinds, engagement, context, ct);
}
