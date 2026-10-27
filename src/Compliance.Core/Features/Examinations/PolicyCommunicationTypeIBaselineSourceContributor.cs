using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.Snapshots;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures point-in-time policy and training campaign audiences and results.</summary>
public sealed class PolicyCommunicationTypeIBaselineSourceContributor(
    ICampaignDirectoryReader directory, PolicyReader policySources, IDomainEventReader events,
    CampaignDirectoryReadConsistency consistency, CampaignSource campaigns, IImmutableContentStore store)
    : ITypeIBaselineSourceContributor
{
    const int PageSize = 200;

    public string Family => "policy_communications";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);



    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct) =>
        PolicySourceFailures.ClassifyAsync(() => ReadCoreAsync(engagement, context, ct));

    async ValueTask<Result<TypeIBaselineSourceContribution>> ReadCoreAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A policy communication baseline read requires its engagement and source context."));

        var receiptCut = context.RecordedThrough is null ? null : context.RequireReceipts(engagement.TenantId);
        var fence = await consistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(fence.Error);

        var effectiveThrough = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        var through = context.RecordedThrough ?? effectiveThrough;
        var policyReferences = context.PreviouslyCapturedSources.Where(static source =>
                source.Kind is "policy_version" or "policy_retirement")
            .GroupBy(static source => source.RecordId)
            .ToDictionary(static group => group.Key, static group => group
                .OrderBy(source => source.Kind, StringComparer.Ordinal).ToArray());
        var trainingSnapshot = await CaptureTrainingCatalogAsync(engagement.TenantId,
            engagement.ProgramId, through, context, ct).ConfigureAwait(false);
        if (!trainingSnapshot.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(trainingSnapshot.Error);

        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        sources.AddRange(trainingSnapshot.Value.Sources);
        var scopeDifferences = new List<string>();
        var policyCache = new Dictionary<Uuid, PolicyReadSnapshot>();
        var seenCampaignIds = new HashSet<Uuid>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await directory.ListProgramAsync(engagement.TenantId,
                engagement.ProgramId, PageSize, cursor, ct).ConfigureAwait(false);
            if (page.Items is null || page.Items.Count > PageSize)
                return InvalidDirectory();

            foreach (var item in page.Items)
            {
                if (!ValidSummary(item, engagement) || !seenCampaignIds.Add(item.CampaignId))
                    return InvalidDirectory();
                if (item.LaunchedAt > effectiveThrough)
                    continue;

                var campaignStream = new PolicyDistributionCampaign(engagement.TenantId, item.CampaignId).Stream;
                var campaignReceipts = receiptCut is null ? null : await receiptCut.ReadAsync(campaignStream, ct).ConfigureAwait(false);
                if (campaignReceipts is not null)
                {
                    var nativeLaunch = campaignReceipts.Records.Select(record => record.Event).OfType<PolicyCampaignLaunched>().SingleOrDefault()
                        ?? throw new ImmutableContentUnavailableException("The listed campaign has no exact native launch fact.");
                    if (!campaignReceipts.Includes(nativeLaunch))
                        continue;
                }
                PolicyReadSnapshot? policy = null;
                if (item.SubjectKind == "policy")
                {
                    if (!policyCache.TryGetValue(item.SubjectId, out policy))
                    {
                        policy = await policySources.ReadAsync(engagement.TenantId, engagement.ProgramId, item.SubjectId, ct).ConfigureAwait(false);
                        if (!policy.IsVisible || policy.Id != item.SubjectId ||
                            policy.ProgramId != engagement.ProgramId)
                            return SourceConflict();
                        var currentReferences = PolicyTypeIBaselineSourceContributor.ReferencesAsOf(
                            engagement.TenantId, policy, engagement.PeriodStart, through,
                            out var validHistory, receiptCut is null ? null : await receiptCut.ReadAsync(
                                new Policy(engagement.TenantId, policy.Id).Stream, ct).ConfigureAwait(false));
                        if (!validHistory || currentReferences.Count == 0 ||
                            policyReferences.TryGetValue(item.SubjectId, out var captured) &&
                            !currentReferences.OrderBy(static source => source.Kind,
                                StringComparer.Ordinal).SequenceEqual(captured))
                            return SourceConflict();
                        policyCache.Add(item.SubjectId, policy);
                    }
                }
                else if (item.SubjectKind == "training")
                {
                    if (!trainingSnapshot.Value.AsOfRequirementIds.Contains(item.SubjectId))
                        return SourceConflict();
                }
                else
                {
                    return InvalidDirectory();
                }

                var history = await ReadCampaignHistoryAsync(engagement.TenantId,
                    engagement.ProgramId, item.CampaignId, ct).ConfigureAwait(false);
                if (!history.IsSuccess)
                    return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
                if (!MatchesSummary(item, history.Value.Launch, history.Value.InitialAudience.Count,
                        history.Value.ClosedAt is not null))
                    return SourceConflict();

                var launch = history.Value.Launch;
                if (campaignReceipts is not null && (!campaignReceipts.Includes(launch) ||
                    !campaignReceipts.Includes(history.Value.Frozen)))
                {
                    blockers.Add($"Campaign {item.SubjectIdentifier} had no complete frozen audience at the recorded cutoff.");
                    continue;
                }
                if (launch.LaunchedAt > effectiveThrough)
                    continue;
                if (launch.Subject.Kind == "policy")
                {
                    if (policy is null || !MatchesPolicySubject(policy, launch, history.Value.Teams))
                        return SourceConflict();
                }
                else if (!MatchesTrainingSubject(trainingSnapshot.Value, launch, history.Value.Teams))
                {
                    return SourceConflict();
                }

                try
                {
                    var acknowledgements = await RecordedResultsAsync(history.Value.Acknowledgements, receiptCut,
                        item => PolicyCampaignParticipant.StreamOf(engagement.TenantId, item.CampaignId, item.PersonId),
                        item => item.AcknowledgedAt <= through, campaignReceipts, ct).ConfigureAwait(false);
                    var completions = await RecordedResultsAsync(history.Value.Completions.Where(item =>
                            item.CompletedOn <= engagement.PeriodStart).ToArray(), receiptCut,
                        item => PolicyCampaignParticipant.StreamOf(engagement.TenantId, item.CampaignId, item.PersonId),
                        item => item.RecordedAt <= through, campaignReceipts, ct).ConfigureAwait(false);
                    var waivers = await RecordedResultsAsync(history.Value.Waivers, receiptCut,
                        item => PolicyCampaignParticipant.StreamOf(engagement.TenantId, item.CampaignId, item.PersonId),
                        item => item.ApprovedAt <= through, campaignReceipts, ct).ConfigureAwait(false);
                    var amendments = history.Value.Amendments.Where(item =>
                            item.Fact.ObservedOn <= engagement.PeriodStart && (campaignReceipts is null
                                ? item.Fact.AmendedAt <= through : campaignReceipts.Includes(item.Fact)))
                        .ToArray();
                    var closure = history.Value.Closure is { } closed && (campaignReceipts is null
                        ? closed.ClosedAt <= through : campaignReceipts.Includes(closed)) ? closed : null;
                    var content = new PolicyCommunicationCampaignSourceContent(
                        engagement.PeriodStart, launch, history.Value.Teams, history.Value.FrozenBatchCount,
                        history.Value.InitialAudience, amendments,
                        acknowledgements, completions, waivers, closure,
                        closure is null ? null : history.Value.ClosingTotals);
                    var capturedEvents = 1 + history.Value.FrozenBatchCount + amendments.Length +
                                         acknowledgements.Length + completions.Length + waivers.Length +
                                         (closure is null ? 0 : 1);
                    sources.Add(new TypeIBaselineSourceReference("policy_communication",
                        item.CampaignId, Uuid.CreateVersion5(item.CampaignId,
                            "policy_communication_v1:" + engagement.PeriodStart.ToString(
                                "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
                        capturedEvents, Digest("bdgrz.snapshot.source.policy_communication.v1\0",
                            content, ComplianceCoreJsonContext.Default
                                .PolicyCommunicationCampaignSourceContent)));
                }
                catch (Exception exception) when ((exception is ArgumentException or JsonException or
                    InvalidOperationException or NotSupportedException) && exception is not ImmutableContentIntegrityException)
                {
                    scopeDifferences.Add(
                        $"Campaign {item.SubjectIdentifier} has invalid canonical communication content.");
                    blockers.Add(
                        $"Campaign {item.SubjectIdentifier} cannot be included in the Type I communication baseline.");
                }
            }

            var next = page.NextCursor;
            if (next is not null && (next == cursor || !seenCursors.Add(next)))
                return InvalidDirectory();
            cursor = next;
        } while (cursor is not null);

        var confirmedCatalog = await CaptureTrainingCatalogAsync(engagement.TenantId,
            engagement.ProgramId, through, context, ct).ConfigureAwait(false);
        if (!confirmedCatalog.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(confirmedCatalog.Error);
        if (CatalogFingerprint(trainingSnapshot.Value.Latest) != CatalogFingerprint(confirmedCatalog.Value.Latest))
            return SourceConflict();

        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(
            engagement.TenantId, fence.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(confirmed.Error);

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(),
                blockers.AsReadOnly(),
                scopeDifferences.AsReadOnly()));
    }

    static async Task<T[]> RecordedResultsAsync<T>(IReadOnlyList<T> facts, RecordedSourceReceiptCut? cut,
        Func<T, EventStreamAddress> sourceStream, Func<T, bool> legacyIncluded,
        RecordedSourceReceiptPrefix? campaignReceipts, CancellationToken ct) where T : DomainEvent, INativeOperationFact
    {
        var selected = new List<T>();
        foreach (var fact in facts)
        {
            var participant = sourceStream(fact);
            if (cut is null ? !legacyIncluded(fact) : !await cut.IncludesAsync(participant, fact, ct).ConfigureAwait(false))
                continue;
            if (cut is not null)
                RequireAdmissionReceipt(fact, participant, campaignReceipts);
            selected.Add(fact);
        }
        return selected.ToArray();
    }

    static void RequireAdmissionReceipt<T>(T fact, EventStreamAddress participant,
        RecordedSourceReceiptPrefix? campaignReceipts) where T : DomainEvent, INativeOperationFact
    {
        if (campaignReceipts is null)
            throw new ImmutableContentUnavailableException("The result's exact campaign admission source is not configured.");
        var causation = fact.Operation.CausationEventId
            ?? throw new ImmutableContentIntegrityException("The participant result has no native admission identity.");
        var admissions = campaignReceipts.Records.Where(record => record.Event.Metadata.EventId == causation).ToArray();
        if (admissions.Length == 0)
            throw new ImmutableContentUnavailableException("The participant result's exact admission is not visible.");
        var intent = fact switch
        {
            PolicyAcknowledgementRecorded acknowledgement => acknowledgement.Intent,
            TrainingCompletionRecorded completion => completion.Intent,
            CampaignWaiverApproved waiver => waiver.Intent,
            _ => throw new ImmutableContentIntegrityException("The participant result has another native result kind."),
        };
        var kind = fact switch
        {
            PolicyAcknowledgementRecorded => nameof(AcknowledgePolicy),
            TrainingCompletionRecorded => nameof(RecordTrainingCompletion),
            CampaignWaiverApproved => nameof(ApproveCampaignWaiver),
            _ => string.Empty,
        };
        if (admissions.Length != 1 || admissions[0].Event is not PolicyParticipantResultAdmitted admitted ||
            admitted.Owner != campaignReceipts.Stream || admitted.Operation.Owner != campaignReceipts.Stream ||
            admitted.Operation.RootOperationId != fact.Operation.RootOperationId || admitted.Operation.CommandKind != kind ||
            admitted.Operation.AffectedOwner != participant || admitted.Operation.ExpectedRevisionOwner != campaignReceipts.Stream ||
            fact.Operation.Owner != participant || fact.Operation.FirstOwner != campaignReceipts.Stream || admitted.Intent != intent)
            throw new ImmutableContentIntegrityException("The participant result differs from its exact campaign admission root, target, or intent.");
        if (!campaignReceipts.Includes(admitted))
            throw new ImmutableContentIntegrityException("The participant result's receipt cutoff excludes its required causal admission.");
    }

    static bool ValidSummary(CampaignSummaryView? summary,
        ExaminationEngagementView engagement) => summary is not null &&
        summary.TenantId == engagement.TenantId && summary.ProgramId == engagement.ProgramId &&
        summary.CampaignId != Uuid.Empty && summary.SubjectId != Uuid.Empty &&
        summary.SubjectKind is "policy" or "training" &&
        !string.IsNullOrWhiteSpace(summary.SubjectIdentifier) &&
        !string.IsNullOrWhiteSpace(summary.Title) && summary.SubjectVersion >= 1 &&
        summary.DueOn != default && summary.Status is "open" or "closed" &&
        summary.LaunchAudience >= 0 && summary.LaunchedAt != default;

    static bool MatchesSummary(CampaignSummaryView summary, PolicyCampaignLaunched launch,
        int audienceCount, bool isClosed) => summary.TenantId == launch.TenantId &&
        summary.ProgramId == launch.ProgramId && summary.CampaignId == launch.CampaignId &&
        summary.SubjectKind == launch.Subject.Kind && summary.SubjectId == launch.Subject.RecordId &&
        summary.SubjectIdentifier == launch.Subject.Identifier &&
        summary.SubjectVersion == launch.Subject.Version && summary.Title == launch.Subject.Title &&
        summary.DueOn == launch.DueOn && summary.Status == (isClosed ? "closed" : "open") &&
        summary.LaunchAudience == audienceCount && summary.LaunchedAt == launch.LaunchedAt;

    static bool MatchesPolicySubject(PolicyReadSnapshot policy, PolicyCampaignLaunched launch, IReadOnlyList<string> teams)
    {
        var subject = launch.Subject;
        var version = policy.FindVersion(subject.Version);
        if (version is null || version.Identifier != subject.Identifier ||
            version.Content.Title != subject.Title ||
            version.ContentSha256 != subject.ContentSha256 ||
            launch.AudienceKind != version.Content.AudienceKind ||
            !teams.SequenceEqual(
                PolicyAudience.CleanTeams(version.Content.AudienceTeams), StringComparer.Ordinal) ||
            version.ApprovedAt > launch.LaunchedAt || version.EffectiveFrom > launch.LaunchedOn)
            return false;

        var effective = policy.ReadVersions().LastOrDefault(candidate =>
            candidate.ApprovedAt <= launch.LaunchedAt && candidate.EffectiveFrom <= launch.LaunchedOn);
        if (effective?.Version != version.Version)
            return false;
        var retirement = policy.ReadDecisions().SingleOrDefault(decision =>
            decision.Kind == "retirement" && decision.Version == version.Version &&
            decision.DecidedAt <= launch.LaunchedAt);
        return retirement is null || version.EffectiveUntil is { } until &&
            until > launch.LaunchedOn;
    }

    static bool MatchesTrainingSubject(TrainingCatalogSnapshot catalog, PolicyCampaignLaunched launch,
        IReadOnlyList<string> teams)
    {
        var subject = launch.Subject;
        var requirement = catalog.Find(subject.RecordId, subject.Version);
        return requirement is not null && requirement.Identifier == subject.Identifier &&
               requirement.Content.CourseName == subject.Title &&
               requirement.ContentSha256 == subject.ContentSha256 &&
               launch.AudienceKind == requirement.Content.AudienceKind &&
               teams.SequenceEqual(PolicyAudience.CleanTeams(requirement.Content.AudienceTeams),
                   StringComparer.Ordinal) &&
               requirement.ChangedAt <= launch.LaunchedAt;
    }

    static Result<CampaignHistory> InvalidHistory() => Result<CampaignHistory>.Failure(
        new RequestError(RequestErrorKind.Conflict,
            "A policy campaign source stream is invalid or inconsistent."));

    /// <summary>
    ///     Reads the campaign owner, its complete audience and amendments from verified immutable content, and the
    ///     results applied to its participant owners. Every fact was validated by its owner's reducers when it was
    ///     replayed; the checks here bind the facts to each other and to the launch.
    /// </summary>
    async ValueTask<Result<CampaignHistory>> ReadCampaignHistoryAsync(Uuid tenantId, Uuid programId, Uuid campaignId,
        CancellationToken ct)
    {
        var loaded = await campaigns.ReadAsync(tenantId, programId, campaignId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<CampaignHistory>.Failure(loaded.Error);
        var state = loaded.Value;
        var campaign = state.Campaign;
        var launch = campaign.Launch!;
        var frozen = campaign.Frozen!;
        var realm = tenantId.ToString();
        if (!IsValidLaunch(launch, tenantId, programId, campaignId, state.Teams))
            return InvalidHistory();

        IReadOnlyList<CampaignAudienceEntry> entries;
        var amendments = new List<PolicyCommunicationAudienceAmendment>();
        var closure = campaign.Closing;
        CampaignTotalsView? closingTotals = null;
        try
        {
            entries = await store.ReadJsonAsync<IReadOnlyList<CampaignAudienceEntry>>(realm, frozen.EntriesManifest, ct)
                .ConfigureAwait(false);
            foreach (var amended in campaign.Amendments)
                amendments.Add(new PolicyCommunicationAudienceAmendment(amended, await store
                    .ReadJsonAsync<IReadOnlyList<CampaignAudienceAmendment>>(realm, amended.AmendmentsManifest, ct)
                    .ConfigureAwait(false)));
            if (closure is not null)
                closingTotals = await store.ReadJsonAsync<CampaignTotalsView>(realm, closure.TotalsContent, ct)
                    .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException)
        {
            return InvalidHistory();
        }

        var audienceIds = new HashSet<Uuid>();
        foreach (var entry in entries)
        {
            if (entry is null || entry.PersonId == Uuid.Empty ||
                string.IsNullOrWhiteSpace(entry.DisplayName) ||
                string.IsNullOrWhiteSpace(entry.WorkerType) || entry.StartDate == default ||
                entry.StartDate > DateOnly.MaxValue.AddDays(-30) ||
                entry.DueOn != (launch.DueOn > entry.StartDate.AddDays(30)
                    ? launch.DueOn
                    : entry.StartDate.AddDays(30)) || !audienceIds.Add(entry.PersonId))
                return InvalidHistory();
        }

        if (entries.Count != launch.AudienceCount || frozen.Batch != 1)
            return InvalidHistory();

        var activePeople = new HashSet<Uuid>(audienceIds);
        var everIncluded = new HashSet<Uuid>(audienceIds);
        long reconciliation = 0;
        foreach (var expanded in amendments)
        {
            var amended = expanded.Fact;
            if (amended.Reconciliation != reconciliation + 1 || !IsValidAmendment(amended, expanded.Items,
                    tenantId, campaignId, launch))
                return InvalidHistory();
            reconciliation = amended.Reconciliation;
            foreach (var amendment in expanded.Items)
            {
                if (amendment.Reason is "joiner" or "mover_in")
                {
                    if (!activePeople.Add(amendment.PersonId))
                        return InvalidHistory();
                    everIncluded.Add(amendment.PersonId);
                }
                else if (!activePeople.Remove(amendment.PersonId))
                {
                    return InvalidHistory();
                }
            }
        }

        var acknowledgements = new List<PolicyAcknowledgementRecorded>();
        var completions = new List<TrainingCompletionRecorded>();
        var waivers = new List<CampaignWaiverApproved>();
        var acknowledgementIds = new HashSet<Uuid>();
        var completionIds = new HashSet<Uuid>();
        foreach (var (personId, participant) in state.Participants.OrderBy(pair => pair.Key.ToString(),
                     StringComparer.Ordinal))
        {
            if (!everIncluded.Contains(personId))
                return InvalidHistory();
            var priorWaivers = new Dictionary<Uuid, CampaignWaiverApproved>();
            foreach (var result in participant.Results.OrderBy(static result => TimeOf(result)))
            {
                switch (result)
                {
                    case PolicyAcknowledgementRecorded acknowledgement:
                        if (closure is not null && acknowledgement.AcknowledgedAt > closure.ClosedAt ||
                            !IsValidAcknowledgement(acknowledgement, launch, acknowledgementIds))
                            return InvalidHistory();
                        acknowledgements.Add(acknowledgement);
                        break;
                    case TrainingCompletionRecorded completion:
                        if (closure is not null && completion.RecordedAt > closure.ClosedAt ||
                            !IsValidCompletion(completion, launch, completionIds))
                            return InvalidHistory();
                        completions.Add(completion);
                        break;
                    case CampaignWaiverApproved waiver:
                        if (closure is not null && waiver.ApprovedAt > closure.ClosedAt ||
                            !IsValidWaiver(waiver, tenantId, campaignId, launch, priorWaivers))
                            return InvalidHistory();
                        priorWaivers[waiver.PersonId] = waiver;
                        waivers.Add(waiver);
                        break;
                    default:
                        return InvalidHistory();
                }
            }
        }

        if (closure is { } closedEvent && (!IsValidClosure(closedEvent, tenantId, campaignId, launch) ||
                                           state.Totals(closedEvent.ClosedOn) != closingTotals))
            return InvalidHistory();
        var view = state.ToView(launch.LaunchedOn);
        var expectedStatus = closure is null ? campaign.IsSealed ? "closing" : "open" : "closed";
        if (view.TenantId != tenantId || view.ProgramId != programId ||
            view.CampaignId != campaignId || view.AudienceKind != launch.AudienceKind ||
            view.RosterSnapshotId != launch.RosterSnapshotId ||
            view.RosterContentSha256 != launch.RosterContentSha256 ||
            view.LaunchedOn != launch.LaunchedOn || view.DueOn != launch.DueOn ||
            view.Instructions != launch.Instructions ||
            view.AcknowledgementText != launch.AcknowledgementText ||
            view.LaunchedBy != launch.Actor || view.LaunchedAt != launch.LaunchedAt ||
            view.Status != expectedStatus || view.Totals.LaunchAudience != launch.AudienceCount)
            return InvalidHistory();

        return Result<CampaignHistory>.Success(new CampaignHistory(launch, frozen, state.Teams, 1,
            entries, amendments.AsReadOnly(),
            acknowledgements.OrderBy(static item => item.AcknowledgedAt).ToArray(),
            completions.OrderBy(static item => item.RecordedAt).ToArray(),
            waivers.OrderBy(static item => item.ApprovedAt).ToArray(), closure, closingTotals));
    }

    static DateTimeOffset TimeOf(DomainEvent result) => result switch
    {
        PolicyAcknowledgementRecorded acknowledgement => acknowledgement.AcknowledgedAt,
        TrainingCompletionRecorded completion => completion.RecordedAt,
        CampaignWaiverApproved waiver => waiver.ApprovedAt,
        _ => DateTimeOffset.MinValue,
    };

    static bool IsValidLaunch(PolicyCampaignLaunched launch, Uuid tenantId, Uuid programId,
        Uuid campaignId, IReadOnlyList<string> teams) => launch.TenantId == tenantId &&
        launch.ProgramId == programId &&
        launch.CampaignId == campaignId && launch.Subject is { } subject &&
        subject.Kind is "policy" or "training" && subject.RecordId != Uuid.Empty &&
        !string.IsNullOrWhiteSpace(subject.Identifier) &&
        !string.IsNullOrWhiteSpace(subject.Title) && subject.Version >= 1 &&
        IsSha256(subject.ContentSha256) && launch.AudienceKind is not null &&
        teams is not null && launch.RosterSnapshotId != Uuid.Empty &&
        IsSha256(launch.RosterContentSha256) && launch.LaunchedOn != default &&
        launch.DueOn >= launch.LaunchedOn && launch.AudienceCount >= 0 && launch.ActorMemberId != Uuid.Empty &&
        launch.Actor is not null && launch.LaunchedAt != default &&
        PolicyAudience.Validate(launch.AudienceKind, teams) is null &&
        teams.SequenceEqual(PolicyAudience.CleanTeams(teams),
            StringComparer.Ordinal) && launch.Instructions?.Length <= 4000 &&
        (launch.Instructions is null || launch.Instructions == launch.Instructions.Trim()) &&
        launch.LaunchedOn == DateOnly.FromDateTime(launch.LaunchedAt.UtcDateTime) &&
        (subject.Kind == "policy"
            ? launch.AcknowledgementText == PolicyDistributionCampaign.DefaultAcknowledgementText
            : launch.AcknowledgementText is null);

    static bool IsValidAmendment(PolicyCampaignAudienceAmended amendment,
        IReadOnlyList<CampaignAudienceAmendment> items, Uuid tenantId, Uuid campaignId,
        PolicyCampaignLaunched launch)
    {
        if (amendment.TenantId != tenantId || amendment.CampaignId != campaignId ||
            amendment.Reconciliation < 1 || amendment.Batch != 1 ||
            amendment.RosterSnapshotId == Uuid.Empty || !IsSha256(amendment.RosterContentSha256) ||
            amendment.ObservedOn == default || amendment.AmendedAt < launch.LaunchedAt ||
            amendment.ObservedOn != DateOnly.FromDateTime(amendment.AmendedAt.UtcDateTime) ||
            amendment.Actor is null)
            return false;
        var seenPeople = new HashSet<Uuid>();
        foreach (var item in items)
        {
            if (item is null || item.PersonId == Uuid.Empty ||
                string.IsNullOrWhiteSpace(item.DisplayName) || !seenPeople.Add(item.PersonId))
                return false;
            if (item.Reason is "joiner" or "mover_in")
            {
                if (string.IsNullOrWhiteSpace(item.WorkerType) || item.DueOn is null)
                    return false;
            }
            else if (item.Reason is "mover_out" or "leaver")
            {
                if (item.WorkerType is not null || item.Department is not null ||
                    item.DueOn is not null)
                    return false;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    static bool IsValidAcknowledgement(PolicyAcknowledgementRecorded acknowledgement,
        PolicyCampaignLaunched launch, HashSet<Uuid> acknowledgementIds) =>
        launch.Subject.Kind == "policy" && acknowledgement.TenantId == launch.TenantId &&
        acknowledgement.CampaignId == launch.CampaignId &&
        acknowledgement.AcknowledgementId != Uuid.Empty && acknowledgement.PersonId != Uuid.Empty &&
        acknowledgement.PolicyId == launch.Subject.RecordId &&
        acknowledgement.PolicyVersion == launch.Subject.Version &&
        acknowledgement.ContentSha256 == launch.Subject.ContentSha256 &&
        acknowledgement.AcknowledgementText == launch.AcknowledgementText &&
        acknowledgement.AcknowledgedAt >= launch.LaunchedAt &&
        acknowledgement.Performer is not null && acknowledgement.Recorder is not null &&
        acknowledgementIds.Add(acknowledgement.AcknowledgementId);

    static bool IsValidCompletion(TrainingCompletionRecorded completion,
        PolicyCampaignLaunched launch, HashSet<Uuid> completionIds) =>
        launch.Subject.Kind == "training" && completion.TenantId == launch.TenantId &&
        completion.CampaignId == launch.CampaignId && completion.CompletionId != Uuid.Empty &&
        completion.PersonId != Uuid.Empty && completion.RequirementId == launch.Subject.RecordId &&
        completion.RequirementVersion == launch.Subject.Version && completion.CompletedOn != default &&
        completion.CompletedOn <= DateOnly.FromDateTime(completion.RecordedAt.UtcDateTime) &&
        completion.Source is "manual" or "lms_export" &&
        !string.IsNullOrWhiteSpace(completion.EvidenceReference) &&
        completion.EvidenceReference == completion.EvidenceReference.Trim() &&
        completion.EvidenceReference.Length <= 1024 && completion.Recorder is not null &&
        completion.RecordedAt >= launch.LaunchedAt && completionIds.Add(completion.CompletionId);

    static bool IsValidWaiver(CampaignWaiverApproved waiver, Uuid tenantId, Uuid campaignId,
        PolicyCampaignLaunched launch, Dictionary<Uuid, CampaignWaiverApproved> priorWaivers)
    {
        if (waiver.TenantId != tenantId || waiver.CampaignId != campaignId ||
            waiver.WaiverId == Uuid.Empty || waiver.PersonId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(waiver.Reason) || waiver.Reason.Length > 4000 ||
            waiver.Reason != waiver.Reason.Trim() || waiver.ApprovedAt < launch.LaunchedAt ||
            waiver.ExpiresOn == default || waiver.Approver is null ||
            waiver.ApproverMemberId == Uuid.Empty || waiver.ApprovedAt == default)
            return false;
        var approvedOn = DateOnly.FromDateTime(waiver.ApprovedAt.UtcDateTime);
        if (waiver.ExpiresOn <= approvedOn || waiver.ExpiresOn > approvedOn.AddMonths(12))
            return false;
        return !priorWaivers.TryGetValue(waiver.PersonId, out var previous) ||
               previous.ExpiresOn <= approvedOn;
    }

    static bool IsValidClosure(PolicyCampaignClosed closure, Uuid tenantId, Uuid campaignId,
        PolicyCampaignLaunched launch) => closure.TenantId == tenantId &&
        closure.CampaignId == campaignId && closure.ClosedOn != default &&
        !string.IsNullOrWhiteSpace(closure.Rationale) && closure.Rationale.Length <= 4000 &&
        closure.Rationale == closure.Rationale.Trim() &&
        closure.Actor is not null && closure.ClosedAt >= launch.LaunchedAt &&
        closure.ClosedOn == DateOnly.FromDateTime(closure.ClosedAt.UtcDateTime);

    /// <summary>
    ///     Captures every training requirement of the program from the requirement owners' own facts, so a lagging
    ///     catalog projection can never hide a requirement or a version.
    /// </summary>
    async ValueTask<Result<TrainingCatalogSnapshot>> CaptureTrainingCatalogAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset through, TypeIBaselineSourceReadContext context, CancellationToken ct)
    {
        var receiptCut = context.RecordedThrough is null ? null : context.RequireReceipts(tenantId);
        var byRequirement = new Dictionary<Uuid, List<TrainingRequirementDefined>>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                           TrainingRequirement.Area), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event is not TrainingRequirementDefined defined || defined.TenantId != tenantId)
                return Result<TrainingCatalogSnapshot>.Failure(InvalidTrainingCatalogError());
            if (defined.ProgramId != programId)
                continue;
            if (!byRequirement.TryGetValue(defined.RequirementId, out var versions))
                byRequirement.Add(defined.RequirementId, versions = []);
            versions.Add(defined);
        }

        var sources = new List<TypeIBaselineSourceReference>();
        var selectedIds = new HashSet<Uuid>();
        var latest = new List<TrainingRequirementView>();
        var versionsById = new Dictionary<(Uuid, long), TrainingRequirementView>();
        foreach (var (requirementId, versions) in byRequirement.OrderBy(pair => pair.Key.ToString(),
                     StringComparer.Ordinal))
        {
            TrainingRequirementView? asOf = null;
            var number = 0L;
            foreach (var fact in versions.OrderBy(static fact => fact.Version))
            {
                TrainingRequirementView version;
                try
                {
                    version = await TrainingRequirementRules.ViewAsync(store, fact, versions.Count, ct)
                        .ConfigureAwait(false);
                }
                catch (ImmutableContentIntegrityException)
                {
                    return Result<TrainingCatalogSnapshot>.Failure(InvalidTrainingCatalogError());
                }

                if (++number != fact.Version || version.RequirementId != requirementId ||
                    version.ProgramId != programId || version.TenantId != tenantId ||
                    TrainingRequirementIdentity.IdFor(tenantId, programId, version.Identifier) != requirementId ||
                    TrainingRequirementIdentity.NormalizeIdentifier(version.Identifier) != version.Identifier ||
                    version.Content is null || version.ContentSha256 != TrainingContentHash(version.Content) ||
                    version.Cadence != TrainingRequirement.Cadence || version.ChangedBy is null ||
                    version.ChangedAt == default)
                    return Result<TrainingCatalogSnapshot>.Failure(InvalidTrainingCatalogError());
                versionsById[(requirementId, version.Version)] = version;
                if (receiptCut is null ? version.ChangedAt <= through : await receiptCut.IncludesAsync(
                        new TrainingRequirement(tenantId, requirementId).Stream, fact, ct).ConfigureAwait(false))
                    asOf = version;
                if (number == versions.Count)
                    latest.Add(version);
            }

            if (asOf is null)
                continue;
            try
            {
                sources.Add(TrainingRequirementReference(asOf));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                InvalidOperationException or NotSupportedException)
            {
                return Result<TrainingCatalogSnapshot>.Failure(InvalidTrainingCatalogError());
            }
            selectedIds.Add(asOf.RequirementId);
        }

        return Result<TrainingCatalogSnapshot>.Success(new TrainingCatalogSnapshot(versionsById, latest,
            sources.AsReadOnly(), selectedIds));
    }

    static TypeIBaselineSourceReference TrainingRequirementReference(
        TrainingRequirementView version)
    {
        var content = new TrainingRequirementSourceContent(version.TenantId, version.ProgramId,
            version.RequirementId, version.Identifier, version.Version, version.Content,
            version.ContentSha256, version.Cadence, version.ChangedBy, version.ChangedAt);
        return new TypeIBaselineSourceReference("training_requirement_version",
            version.RequirementId, Uuid.CreateVersion5(version.RequirementId,
                $"training_requirement_version_v1:{version.Version}"), version.Version,
            Digest("bdgrz.snapshot.source.training_requirement_version.v1\0", content,
                ComplianceCoreJsonContext.Default.TrainingRequirementSourceContent));
    }

    static string TrainingContentHash(TrainingRequirementContent content) =>
        Convert.ToHexStringLower(SnapshotSourceCapture.HashData(JsonSerializer.SerializeToUtf8Bytes(content,
            ComplianceCoreJsonContext.Default.TrainingRequirementContent)));

    static string CatalogFingerprint(IReadOnlyList<TrainingRequirementView> latest)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var item in latest.OrderBy(static item => item.RequirementId.ToString(),
                     StringComparer.Ordinal))
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(item,
                ComplianceCoreJsonContext.Default.TrainingRequirementView);
            var length = BitConverter.GetBytes(bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    static string Digest<T>(string domain, T content,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var element = JsonSerializer.SerializeToElement(content, typeInfo);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
            WriteCanonical(writer, element);
        var prefix = Encoding.UTF8.GetBytes(domain);
        var bytes = stream.ToArray();
        var material = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(material, 0);
        bytes.CopyTo(material, prefix.Length);
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }

    static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name.Normalize(NormalizationForm.FormC));
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString()!.Normalize(NormalizationForm.FormC));
                break;
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out var number))
                    throw new InvalidOperationException(
                        "Policy communication baseline numbers must be signed 64-bit integers.");
                writer.WriteNumberValue(number);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException(
                    "Unsupported content entered a policy communication baseline.");
        }
    }

    static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static RequestError InvalidTrainingCatalogError() => new(RequestErrorKind.Conflict,
        "The training requirement catalog is invalid or inconsistent.");

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The policy campaign directory returned an invalid or unstable program page."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "A policy communication source changed or did not match its source directory. Retry the preview.",
            isTransient: true));

    sealed record TrainingCatalogSnapshot(IReadOnlyDictionary<(Uuid, long), TrainingRequirementView> Versions,
        IReadOnlyList<TrainingRequirementView> Latest,
        IReadOnlyList<TypeIBaselineSourceReference> Sources,
        IReadOnlySet<Uuid> AsOfRequirementIds)
    {
        public TrainingRequirementView? Find(Uuid requirementId, long version) =>
            Versions.GetValueOrDefault((requirementId, version));
    }

    sealed record CampaignHistory(PolicyCampaignLaunched Launch, PolicyCampaignAudienceFrozen Frozen, IReadOnlyList<string> Teams, int FrozenBatchCount,
        IReadOnlyList<CampaignAudienceEntry> InitialAudience,
        IReadOnlyList<PolicyCommunicationAudienceAmendment> Amendments,
        IReadOnlyList<PolicyAcknowledgementRecorded> Acknowledgements,
        IReadOnlyList<TrainingCompletionRecorded> Completions,
        IReadOnlyList<CampaignWaiverApproved> Waivers, PolicyCampaignClosed? Closure,
        CampaignTotalsView? ClosingTotals)
    {
        public DateTimeOffset? ClosedAt => Closure?.ClosedAt;
    }
}
