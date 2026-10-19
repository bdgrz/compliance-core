using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures point-in-time policy and training campaign audiences and results.</summary>
public sealed class PolicyCommunicationTypeIBaselineSourceContributor(
    ICampaignDirectoryReader directory, IAggregateReader reader, IDomainEventReader events,
    CampaignDirectoryReadConsistency consistency) : ITypeIBaselineSourceContributor
{
    const int PageSize = 200;

    public string Family => "policy_communications";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A policy communication baseline read requires its engagement and source context."));

        var fence = await consistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(fence.Error);

        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var policyReferences = context.PreviouslyCapturedSources.Where(static source =>
                source.Kind is "policy_version" or "policy_retirement")
            .GroupBy(static source => source.RecordId)
            .ToDictionary(static group => group.Key, static group => group
                .OrderBy(source => source.Kind, StringComparer.Ordinal).ToArray());
        var trainingCatalog = await reader.HydrateAsync(new TrainingCatalog(engagement.TenantId,
            engagement.ProgramId), ct).ConfigureAwait(false);
        var trainingSnapshot = CaptureTrainingCatalog(engagement.TenantId,
            engagement.ProgramId, trainingCatalog, through);
        if (!trainingSnapshot.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(trainingSnapshot.Error);

        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        sources.AddRange(trainingSnapshot.Value.Sources);
        var scopeDifferences = new List<string>();
        var policyCache = new Dictionary<Uuid, Policy>();
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
                if (item.LaunchedAt > through)
                    continue;

                Policy? policy = null;
                if (item.SubjectKind == "policy")
                {
                    if (!policyCache.TryGetValue(item.SubjectId, out policy))
                    {
                        policy = await reader.HydrateAsync(new Policy(engagement.TenantId,
                            item.SubjectId), ct).ConfigureAwait(false);
                        if (!policy.IsVisible || policy.Id != item.SubjectId ||
                            policy.ProgramId != engagement.ProgramId)
                            return SourceConflict();
                        var currentReferences = PolicyTypeIBaselineSourceContributor.ReferencesAsOf(
                            engagement.TenantId, policy, engagement.PeriodStart, through,
                            out var validHistory);
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

                var stream = new EventStreamAddress(engagement.TenantId.ToString(),
                    "policy-distribution-campaigns", item.CampaignId.ToString());
                var history = await ReadCampaignHistoryAsync(stream, engagement.TenantId,
                    engagement.ProgramId, item.CampaignId, ct).ConfigureAwait(false);
                if (!history.IsSuccess)
                    return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
                if (!MatchesSummary(item, history.Value.Launch, history.Value.InitialAudience.Count,
                        history.Value.ClosedAt is not null))
                    return SourceConflict();

                var launch = history.Value.Launch;
                if (launch.LaunchedAt > through)
                    continue;
                if (launch.Subject.Kind == "policy")
                {
                    if (policy is null || !MatchesPolicySubject(policy, launch))
                        return SourceConflict();
                }
                else if (!MatchesTrainingSubject(trainingCatalog, launch))
                {
                    return SourceConflict();
                }

                try
                {
                    var acknowledgements = history.Value.Acknowledgements.Where(item =>
                            item.AcknowledgedAt <= through)
                        .ToArray();
                    var completions = history.Value.Completions.Where(item =>
                            item.CompletedOn <= engagement.PeriodStart && item.RecordedAt <= through)
                        .ToArray();
                    var waivers = history.Value.Waivers.Where(item => item.ApprovedAt <= through)
                        .ToArray();
                    var amendments = history.Value.Amendments.Where(item =>
                            item.AmendedAt <= through && item.ObservedOn <= engagement.PeriodStart)
                        .ToArray();
                    var closure = history.Value.Closure is { } closed && closed.ClosedAt <= through
                        ? closed
                        : null;
                    var content = new PolicyCommunicationCampaignSourceContent(
                        engagement.PeriodStart, launch, history.Value.FrozenBatchCount,
                        history.Value.InitialAudience, amendments, acknowledgements, completions,
                        waivers, closure);
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
                catch (Exception exception) when (exception is ArgumentException or JsonException or
                    InvalidOperationException or NotSupportedException)
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

        var currentCatalog = await reader.HydrateAsync(new TrainingCatalog(engagement.TenantId,
            engagement.ProgramId), ct).ConfigureAwait(false);
        var confirmedCatalog = CaptureTrainingCatalog(engagement.TenantId,
            engagement.ProgramId, currentCatalog, through);
        if (!confirmedCatalog.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(confirmedCatalog.Error);
        if (CatalogFingerprint(trainingSnapshot.Value.Catalog.Latest()) !=
            CatalogFingerprint(confirmedCatalog.Value.Catalog.Latest()))
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

    static bool MatchesPolicySubject(Policy policy, PolicyCampaignLaunched launch)
    {
        var subject = launch.Subject;
        var version = policy.FindVersion(subject.Version);
        if (version is null || version.Identifier != subject.Identifier ||
            version.Content.Title != subject.Title ||
            version.ContentSha256 != subject.ContentSha256 ||
            launch.AudienceKind != version.Content.AudienceKind ||
            !launch.AudienceTeams.SequenceEqual(
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

    static bool MatchesTrainingSubject(TrainingCatalog catalog, PolicyCampaignLaunched launch)
    {
        var subject = launch.Subject;
        var requirement = catalog.Find(subject.RecordId, subject.Version);
        return requirement is not null && requirement.Identifier == subject.Identifier &&
               requirement.Content.CourseName == subject.Title &&
               requirement.ContentSha256 == subject.ContentSha256 &&
               launch.AudienceKind == requirement.Content.AudienceKind &&
               launch.AudienceTeams.SequenceEqual(
                   PolicyAudience.CleanTeams(requirement.Content.AudienceTeams),
                   StringComparer.Ordinal) &&
               requirement.ChangedAt <= launch.LaunchedAt;
    }

    static Result<CampaignHistory> InvalidHistory() => Result<CampaignHistory>.Failure(
        new RequestError(RequestErrorKind.Conflict,
            "A policy campaign source stream is invalid or inconsistent."));

    async ValueTask<Result<CampaignHistory>> ReadCampaignHistoryAsync(EventStreamAddress stream,
        Uuid tenantId, Uuid programId, Uuid campaignId, CancellationToken ct)
    {
        var history = new List<DomainEvent>();
        var seenEventIds = new HashSet<Uuid>();
        ulong expectedVersion = 1;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            var metadata = record.Event?.Metadata;
            if (record.Stream != stream || record.Event is null || metadata is null ||
                metadata.EventId == Uuid.Empty || !seenEventIds.Add(metadata.EventId) ||
                metadata.AggregateId != campaignId || metadata.AggregateVersion != expectedVersion ||
                metadata.OccurredOn == default)
                return InvalidHistory();
            history.Add(record.Event);
            expectedVersion++;
        }

        if (history.Count == 0 || history[0] is not PolicyCampaignLaunched launch ||
            !IsValidLaunch(launch, tenantId, programId, campaignId))
            return InvalidHistory();

        var index = 1;
        var initialAudience = new List<CampaignAudienceEntry>();
        var audienceIds = new HashSet<Uuid>();
        var batch = 0;
        while (index < history.Count && history[index] is PolicyCampaignAudienceFrozen frozen)
        {
            batch++;
            if (frozen.TenantId != tenantId || frozen.CampaignId != campaignId ||
                frozen.Batch != batch || frozen.Entries is null || frozen.Entries.Count is < 1 or
                > PolicyDistributionCampaign.BatchSize)
                return InvalidHistory();
            foreach (var entry in frozen.Entries)
            {
                if (entry is null || entry.PersonId == Uuid.Empty ||
                    string.IsNullOrWhiteSpace(entry.DisplayName) ||
                    string.IsNullOrWhiteSpace(entry.WorkerType) || entry.StartDate == default ||
                    entry.StartDate > DateOnly.MaxValue.AddDays(-30) ||
                    entry.DueOn != (launch.DueOn > entry.StartDate.AddDays(30)
                        ? launch.DueOn
                        : entry.StartDate.AddDays(30)) || !audienceIds.Add(entry.PersonId))
                    return InvalidHistory();
                initialAudience.Add(entry);
            }
            index++;
        }
        if (initialAudience.Count != launch.AudienceCount ||
            (launch.AudienceCount == 0) != (batch == 0))
            return InvalidHistory();

        var activePeople = new HashSet<Uuid>(audienceIds);
        var acknowledgementPeople = new HashSet<Uuid>();
        var completionPeople = new HashSet<Uuid>();
        var acknowledgementIds = new HashSet<Uuid>();
        var completionIds = new HashSet<Uuid>();
        var waiversByPerson = new Dictionary<Uuid, CampaignWaiverApproved>();
        var amendments = new List<PolicyCampaignAudienceAmended>();
        var acknowledgements = new List<PolicyAcknowledgementRecorded>();
        var completions = new List<TrainingCompletionRecorded>();
        var waivers = new List<CampaignWaiverApproved>();
        PolicyCampaignClosed? closure = null;
        long reconciliation = 0;
        var amendmentBatch = 0;
        PolicyCampaignAudienceAmended? priorAmendment = null;

        while (index < history.Count)
        {
            var domainEvent = history[index++];
            switch (domainEvent)
            {
                case PolicyCampaignAudienceAmended amended:
                    if (closure is not null ||
                        !IsValidAmendment(amended, tenantId, campaignId, launch))
                        return InvalidHistory();
                    if (amended.Reconciliation == reconciliation)
                    {
                        if (priorAmendment is null || amended.Batch != amendmentBatch + 1 ||
                            !SameAmendmentBatch(priorAmendment, amended))
                            return InvalidHistory();
                    }
                    else
                    {
                        if (amended.Reconciliation != reconciliation + 1 || amended.Batch != 1)
                            return InvalidHistory();
                        reconciliation = amended.Reconciliation;
                    }
                    amendmentBatch = amended.Batch;
                    priorAmendment = amended;
                    foreach (var amendment in amended.Amendments)
                    {
                        if (amendment.Reason is "joiner" or "mover_in")
                        {
                            if (!activePeople.Add(amendment.PersonId))
                                return InvalidHistory();
                        }
                        else if (!activePeople.Remove(amendment.PersonId))
                        {
                            return InvalidHistory();
                        }
                    }
                    amendments.Add(amended);
                    break;
                case PolicyAcknowledgementRecorded acknowledgement:
                    priorAmendment = null;
                    if (closure is not null || !IsValidAcknowledgement(acknowledgement, launch,
                            activePeople, acknowledgementPeople, acknowledgementIds))
                        return InvalidHistory();
                    acknowledgements.Add(acknowledgement);
                    break;
                case TrainingCompletionRecorded completion:
                    priorAmendment = null;
                    if (closure is not null || !IsValidCompletion(completion, launch,
                            activePeople, completionPeople, completionIds))
                        return InvalidHistory();
                    completions.Add(completion);
                    break;
                case CampaignWaiverApproved waiver:
                    priorAmendment = null;
                    if (closure is not null || !IsValidWaiver(waiver, tenantId, campaignId,
                            launch, activePeople, acknowledgementPeople, completionPeople,
                            waiversByPerson))
                        return InvalidHistory();
                    waiversByPerson[waiver.PersonId] = waiver;
                    waivers.Add(waiver);
                    break;
                case PolicyCampaignClosed closed:
                    priorAmendment = null;
                    if (closure is not null || !IsValidClosure(closed, tenantId, campaignId,
                            launch))
                        return InvalidHistory();
                    closure = closed;
                    break;
                default:
                    return InvalidHistory();
            }
        }

        var campaign = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId,
            campaignId), ct).ConfigureAwait(false);
        if (!campaign.IsLaunched || campaign.ProgramId != programId ||
            campaign.Subject != launch.Subject || campaign.Id != campaignId)
            return InvalidHistory();
        if (closure is { } closedEvent && campaign.Totals(closedEvent.ClosedOn) != closedEvent.Totals)
            return InvalidHistory();
        var view = campaign.ToView(launch.LaunchedOn);
        var expectedStatus = closure is null ? "open" : "closed";
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

        return Result<CampaignHistory>.Success(new CampaignHistory(launch, batch,
            initialAudience.AsReadOnly(), amendments.AsReadOnly(), acknowledgements.AsReadOnly(),
            completions.AsReadOnly(), waivers.AsReadOnly(), closure));
    }

    static bool IsValidLaunch(PolicyCampaignLaunched launch, Uuid tenantId, Uuid programId,
        Uuid campaignId) => launch.TenantId == tenantId && launch.ProgramId == programId &&
        launch.CampaignId == campaignId && launch.Subject is { } subject &&
        subject.Kind is "policy" or "training" && subject.RecordId != Uuid.Empty &&
        !string.IsNullOrWhiteSpace(subject.Identifier) &&
        !string.IsNullOrWhiteSpace(subject.Title) && subject.Version >= 1 &&
        IsSha256(subject.ContentSha256) && launch.AudienceKind is not null &&
        launch.AudienceTeams is not null && launch.RosterSnapshotId != Uuid.Empty &&
        IsSha256(launch.RosterContentSha256) && launch.LaunchedOn != default &&
        launch.DueOn >= launch.LaunchedOn && launch.AudienceCount is >= 0 and
        <= PolicyDistributionCampaign.MaximumAudience && launch.ActorMemberId != Uuid.Empty &&
        launch.Actor is not null && launch.LaunchedAt != default &&
        PolicyAudience.Validate(launch.AudienceKind, launch.AudienceTeams) is null &&
        launch.AudienceTeams.SequenceEqual(PolicyAudience.CleanTeams(launch.AudienceTeams),
            StringComparer.Ordinal) && launch.Instructions?.Length <= 4000 &&
        (launch.Instructions is null || launch.Instructions == launch.Instructions.Trim()) &&
        launch.LaunchedOn == DateOnly.FromDateTime(launch.LaunchedAt.UtcDateTime) &&
        (subject.Kind == "policy"
            ? launch.AcknowledgementText == PolicyDistributionCampaign.DefaultAcknowledgementText
            : launch.AcknowledgementText is null);

    static bool IsValidAmendment(PolicyCampaignAudienceAmended amendment, Uuid tenantId,
        Uuid campaignId, PolicyCampaignLaunched launch)
    {
        if (amendment.TenantId != tenantId || amendment.CampaignId != campaignId ||
            amendment.Reconciliation < 1 || amendment.Batch < 1 ||
            amendment.RosterSnapshotId == Uuid.Empty || !IsSha256(amendment.RosterContentSha256) ||
            amendment.ObservedOn == default || amendment.AmendedAt < launch.LaunchedAt ||
            amendment.ObservedOn != DateOnly.FromDateTime(amendment.AmendedAt.UtcDateTime) ||
            amendment.Actor is null || amendment.Amendments is null ||
            amendment.Amendments.Count > PolicyDistributionCampaign.BatchSize)
            return false;
        var seenPeople = new HashSet<Uuid>();
        foreach (var item in amendment.Amendments)
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

    static bool SameAmendmentBatch(PolicyCampaignAudienceAmended left,
        PolicyCampaignAudienceAmended right) =>
        left.Reconciliation == right.Reconciliation && left.RosterSnapshotId == right.RosterSnapshotId &&
        left.RosterContentSha256 == right.RosterContentSha256 &&
        left.ObservedOn == right.ObservedOn && left.Actor == right.Actor &&
        left.AmendedAt == right.AmendedAt;

    static bool IsValidAcknowledgement(PolicyAcknowledgementRecorded acknowledgement,
        PolicyCampaignLaunched launch, HashSet<Uuid> activePeople,
        HashSet<Uuid> acknowledgementPeople, HashSet<Uuid> acknowledgementIds) =>
        launch.Subject.Kind == "policy" && acknowledgement.TenantId == launch.TenantId &&
        acknowledgement.CampaignId == launch.CampaignId &&
        acknowledgement.AcknowledgementId != Uuid.Empty && acknowledgement.PersonId != Uuid.Empty &&
        acknowledgement.PolicyId == launch.Subject.RecordId &&
        acknowledgement.PolicyVersion == launch.Subject.Version &&
        acknowledgement.ContentSha256 == launch.Subject.ContentSha256 &&
        acknowledgement.AcknowledgementText == launch.AcknowledgementText &&
        acknowledgement.AcknowledgedAt >= launch.LaunchedAt &&
        acknowledgement.Performer is not null && acknowledgement.Recorder is not null &&
        activePeople.Contains(acknowledgement.PersonId) &&
        acknowledgementPeople.Add(acknowledgement.PersonId) &&
        acknowledgementIds.Add(acknowledgement.AcknowledgementId);

    static bool IsValidCompletion(TrainingCompletionRecorded completion,
        PolicyCampaignLaunched launch, HashSet<Uuid> activePeople,
        HashSet<Uuid> completionPeople, HashSet<Uuid> completionIds) =>
        launch.Subject.Kind == "training" && completion.TenantId == launch.TenantId &&
        completion.CampaignId == launch.CampaignId && completion.CompletionId != Uuid.Empty &&
        completion.PersonId != Uuid.Empty && completion.RequirementId == launch.Subject.RecordId &&
        completion.RequirementVersion == launch.Subject.Version && completion.CompletedOn != default &&
        completion.CompletedOn <= DateOnly.FromDateTime(completion.RecordedAt.UtcDateTime) &&
        completion.Source is "manual" or "lms_export" &&
        !string.IsNullOrWhiteSpace(completion.EvidenceReference) &&
        completion.EvidenceReference == completion.EvidenceReference.Trim() &&
        completion.EvidenceReference.Length <= 1024 && completion.Recorder is not null &&
        completion.RecordedAt >= launch.LaunchedAt && activePeople.Contains(completion.PersonId) &&
        completionPeople.Add(completion.PersonId) && completionIds.Add(completion.CompletionId);

    static bool IsValidWaiver(CampaignWaiverApproved waiver, Uuid tenantId, Uuid campaignId,
        PolicyCampaignLaunched launch, HashSet<Uuid> activePeople,
        HashSet<Uuid> acknowledgementPeople,
        HashSet<Uuid> completionPeople, Dictionary<Uuid, CampaignWaiverApproved> priorWaivers)
    {
        if (waiver.TenantId != tenantId || waiver.CampaignId != campaignId ||
            waiver.WaiverId == Uuid.Empty || waiver.PersonId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(waiver.Reason) || waiver.Reason.Length > 4000 ||
            waiver.Reason != waiver.Reason.Trim() || waiver.ApprovedAt < launch.LaunchedAt ||
            waiver.ExpiresOn == default || waiver.Approver is null ||
            waiver.ApproverMemberId == Uuid.Empty || waiver.ApprovedAt == default ||
            !activePeople.Contains(waiver.PersonId) ||
            acknowledgementPeople.Contains(waiver.PersonId) ||
            completionPeople.Contains(waiver.PersonId))
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
        closure.ClosedOn == DateOnly.FromDateTime(closure.ClosedAt.UtcDateTime) &&
        closure.Totals is not null;

    static Result<TrainingCatalogSnapshot> CaptureTrainingCatalog(
        Uuid tenantId, Uuid programId, TrainingCatalog catalog, DateTimeOffset through)
    {
        var sources = new List<TypeIBaselineSourceReference>();
        var selectedIds = new HashSet<Uuid>();
        var latest = catalog.Latest();
        var requirementIds = new HashSet<Uuid>();
        foreach (var item in latest)
        {
            if (item is null || item.TenantId != tenantId || item.ProgramId != programId ||
                item.RequirementId == Uuid.Empty ||
                TrainingCatalog.NormalizeIdentifier(item.Identifier) != item.Identifier ||
                item.Version < 1 || item.Version != item.LatestVersion ||
                !requirementIds.Add(item.RequirementId))
                return Result<TrainingCatalogSnapshot>.Failure(InvalidTrainingCatalogError());

            TrainingRequirementView? asOf = null;
            for (var versionNumber = 1L; versionNumber <= item.LatestVersion; versionNumber++)
            {
                var version = catalog.Find(item.RequirementId, versionNumber);
                if (version is null || version.TenantId != tenantId ||
                    version.ProgramId != programId || version.RequirementId != item.RequirementId ||
                    version.Identifier != item.Identifier || version.Version != versionNumber ||
                    version.LatestVersion != item.LatestVersion ||
                    version.Content is null ||
                    version.ContentSha256 != TrainingContentHash(version.Content) ||
                    version.Cadence != TrainingCatalog.Cadence || version.ChangedBy is null ||
                    version.ChangedAt == default)
                    return Result<TrainingCatalogSnapshot>.Failure(InvalidTrainingCatalogError());
                if (version.ChangedAt <= through)
                    asOf = version;
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

        return Result<TrainingCatalogSnapshot>.Success(new TrainingCatalogSnapshot(catalog,
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
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content,
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
        return Convert.ToHexStringLower(SHA256.HashData(material));
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

    sealed record TrainingCatalogSnapshot(TrainingCatalog Catalog,
        IReadOnlyList<TypeIBaselineSourceReference> Sources,
        IReadOnlySet<Uuid> AsOfRequirementIds);

    sealed record CampaignHistory(PolicyCampaignLaunched Launch, int FrozenBatchCount,
        IReadOnlyList<CampaignAudienceEntry> InitialAudience,
        IReadOnlyList<PolicyCampaignAudienceAmended> Amendments,
        IReadOnlyList<PolicyAcknowledgementRecorded> Acknowledgements,
        IReadOnlyList<TrainingCompletionRecorded> Completions,
        IReadOnlyList<CampaignWaiverApproved> Waivers, PolicyCampaignClosed? Closure)
    {
        public DateTimeOffset? ClosedAt => Closure?.ClosedAt;
    }
}
