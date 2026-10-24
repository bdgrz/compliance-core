using Bdgrz.Compliance.Features.Snapshots;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures the exact evidence linked to a Type I source and its point-in-time governance.</summary>
public sealed class EvidenceGovernanceTypeIBaselineSourceContributor(
    IDomainEventReader events, IAggregateReader aggregates) : ITypeIBaselineSourceContributor
{
    const int MaximumEvents = 100_000;
    const string SupportArea = "evidence-support";
    const string DigestDomain = "bdgrz.snapshot.source.evidence_governance.v1\0";

    public string Family => "evidence_governance";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context?.PreviouslyCapturedSources is null ||
            engagement.TenantId == Uuid.Empty || engagement.ProgramId == Uuid.Empty ||
            engagement.PeriodStart == default)
            return Failure(RequestErrorKind.Validation,
                "An evidence-governance baseline read requires its engagement and source context.");

        var effectiveSources = ReadEffectiveSupportedSources(context.PreviouslyCapturedSources);
        if (!effectiveSources.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(effectiveSources.Error);

        var through = context.RecordedThrough ?? new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var supportPattern = EventStreamPattern.ForPattern(engagement.TenantId.ToString(), SupportArea);
        var supportHistory = await ReadPatternTwiceAsync(supportPattern, ct).ConfigureAwait(false);
        if (!supportHistory.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(supportHistory.Error);

        var supportLinks = new List<EvidenceSupportLinked>();
        foreach (var record in supportHistory.Value)
        {
            if (!ValidSupportRecord(record, engagement.TenantId))
                return InvalidHistory();
            var link = (EvidenceSupportLinked)record.Event!;
            if (link.ProgramId != engagement.ProgramId || link.OccurredAt > through ||
                record.Event!.Metadata!.OccurredOn > through ||
                link.PeriodStart > engagement.PeriodStart || link.PeriodEnd < engagement.PeriodStart ||
                !effectiveSources.Value.Contains((link.SupportedRecordType!,
                    link.SupportedRecordId, link.SupportedRecordRevision)))
                continue;
            supportLinks.Add(link);
        }

        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        var sources = new List<TypeIBaselineSourceReference>();
        var artifactIds = supportLinks.Select(static link => link.ArtifactId).ToHashSet();
        var related = await ReadRelatedGovernanceAsync(engagement, artifactIds, through, ct)
            .ConfigureAwait(false);
        if (!related.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(related.Error);
        foreach (var artifactGroup in supportLinks.GroupBy(static link => link.ArtifactId)
                     .OrderBy(static group => group.Key.ToString(), StringComparer.Ordinal))
        {
            var artifact = await ReadArtifactAsOfAsync(engagement.TenantId, artifactGroup.Key,
                through, ct).ConfigureAwait(false);
            if (!artifact.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(artifact.Error);

            if (artifact.Value.AsOfRevision == 0 || artifact.Value.Registration is null)
                return InvalidHistory();
            if (artifactGroup.Any(link => link.ArtifactContentSha256 !=
                    artifact.Value.Registration.ContentSha256 ||
                    link.ArtifactRevision < artifact.Value.Events.Count(item =>
                        item.OccurredOn < link.OccurredAt) ||
                    link.ArtifactRevision > artifact.Value.Events.Count(item =>
                        item.OccurredOn <= link.OccurredAt)))
                return InvalidHistory();
            if (artifact.Value.State != EvidenceArtifactStates.Available)
                blockers.Add($"Evidence artifact {artifactGroup.Key} was not available on the Type I date.");
            if (artifact.Value.Registration.Content.PeriodStart > engagement.PeriodStart ||
                artifact.Value.Registration.Content.PeriodEnd < engagement.PeriodStart)
                blockers.Add($"Evidence artifact {artifactGroup.Key} does not cover the Type I date.");

            var supportEvents = artifactGroup.OrderBy(static link => link.OccurredAt)
                .ThenBy(static link => link.SubjectId.ToString(), StringComparer.Ordinal)
                .Select(link => Capture(link, SupportArea, link.SubjectId,
                    link.Metadata!)).ToArray();
            if (supportEvents.Any(static item => item is null))
                return InvalidHistory();

            var policy = await ReadHandlingPolicyAsOfAsync(engagement.TenantId,
                artifactGroup.Key, through, artifact.Value.AsOfRevision, ct).ConfigureAwait(false);
            if (!policy.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(policy.Error);
            if (policy.Value is null)
                blockers.Add($"Evidence artifact {artifactGroup.Key} has no handling policy recorded on the Type I date.");
            else if (policy.Value.Source.ContentSha256 != artifact.Value.Registration.ContentSha256 ||
                     policy.Value.SourceHandlingClass != artifact.Value.Registration.Content.HandlingClass ||
                     policy.Value.SourceState != artifact.Value.State)
                blockers.Add($"Evidence artifact {artifactGroup.Key} has a handling policy bound to different source facts.");

            var retention = await ReadRetentionAsOfAsync(engagement.TenantId,
                artifactGroup.Key, through, ct).ConfigureAwait(false);
            if (!retention.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(retention.Error);
            if (retention.Value.Basis is null)
                blockers.Add($"Evidence artifact {artifactGroup.Key} has no retention basis recorded on the Type I date.");
            if (retention.Value.Holds.Any(static hold => hold.ReleasedAt is null))
                scopeDifferences.Add($"Evidence artifact {artifactGroup.Key} was subject to an active legal hold on the Type I date.");

            var content = new EvidenceGovernanceTypeIBaselineSourceContent(engagement.PeriodStart,
                engagement.ProgramId, artifactGroup.Key, artifact.Value.AsOfRevision,
                artifact.Value.Registration, artifact.Value.State!, artifact.Value.StateReason,
                artifact.Value.Events, Array.AsReadOnly(supportEvents.Select(static item => item!)
                    .ToArray()), related.Value.ByArtifact.GetValueOrDefault(artifactGroup.Key) ?? [],
                policy.Value, retention.Value.Basis, retention.Value.Holds);
            try
            {
                sources.Add(new TypeIBaselineSourceReference("evidence_artifact",
                    artifactGroup.Key, Uuid.CreateVersion5(artifactGroup.Key,
                        "evidence_governance_v1:" + engagement.PeriodStart.ToString(
                            "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
                    artifact.Value.AsOfRevision, Digest(content)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                InvalidOperationException or NotSupportedException)
            {
                blockers.Add($"Evidence artifact {artifactGroup.Key} has invalid canonical governance content.");
                scopeDifferences.Add($"Evidence artifact {artifactGroup.Key} has invalid canonical governance content.");
            }
        }

        foreach (var hold in related.Value.EngagementHolds.OrderBy(static pair =>
                     pair.Key.ToString(), StringComparer.Ordinal))
        {
            var history = hold.Value.OrderBy(static item => item.AggregateVersion).ToArray();
            var last = history.LastOrDefault();
            if (last is null)
                continue;
            var content = new EvidenceGovernanceHoldSourceContent(engagement.PeriodStart,
                engagement.ExaminationEngagementId, hold.Key, Array.AsReadOnly(history));
            try
            {
                sources.Add(new TypeIBaselineSourceReference("artifact_engagement_hold",
                    hold.Key, Uuid.CreateVersion5(hold.Key, "artifact_engagement_hold_v1:" +
                        engagement.PeriodStart.ToString("yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture)),
                    checked((long)last.AggregateVersion), Digest(content)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                InvalidOperationException or NotSupportedException or OverflowException)
            {
                blockers.Add("An engagement evidence hold has invalid canonical source content.");
                scopeDifferences.Add("An engagement evidence hold has invalid canonical source content.");
            }
        }

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(), blockers.AsReadOnly(),
                scopeDifferences.AsReadOnly()));
    }

    static Result<HashSet<(string Kind, Uuid RecordId, long Revision)>> ReadEffectiveSupportedSources(
        IReadOnlyList<TypeIBaselineSourceReference> sources)
    {
        var supported = new HashSet<(string Kind, Uuid RecordId, long Revision)>();
        foreach (var source in sources)
        {
            if (source is null || source.RecordId == Uuid.Empty || source.Revision < 1 ||
                !IsDigest(source.ContentSha256))
                return Result<HashSet<(string Kind, Uuid RecordId, long Revision)>>.Failure(
                    new RequestError(RequestErrorKind.Conflict,
                        "An earlier Type I source reference is invalid."));
            if (source.Kind is "control" or "control_version" or "control_evaluation")
                supported.Add((source.Kind, source.RecordId, source.Revision));
        }
        return Result<HashSet<(string Kind, Uuid RecordId, long Revision)>>.Success(supported);
    }

    async ValueTask<Result<ArtifactAsOf>> ReadArtifactAsOfAsync(Uuid tenantId, Uuid artifactId,
        DateTimeOffset through, CancellationToken ct)
    {
        var stream = EvidenceStreams.Address(tenantId, artifactId);
        var first = await ReadStreamTwiceAsync(stream, ct).ConfigureAwait(false);
        if (!first.IsSuccess)
            return Result<ArtifactAsOf>.Failure(first.Error);

        EvidenceArtifactRegistered? currentRegistration = null;
        string? currentState = null;
        string? currentReason = null;
        EvidenceArtifactRegistered? registration = null;
        string? state = null;
        string? reason = null;
        long asOfRevision = 0;
        var asOfEvents = new List<EvidenceGovernanceSourceEvent>();
        var eventIds = new HashSet<Uuid>();
        DateTimeOffset? priorOccurredAt = null;
        for (var index = 0; index < first.Value.Count; index++)
        {
            var item = first.Value[index];
            if (item.Stream != stream || item.Event?.Metadata is not { } metadata ||
                metadata.EventId == Uuid.Empty || !eventIds.Add(metadata.EventId) ||
                metadata.AggregateId != artifactId || metadata.AggregateVersion != (ulong)index + 1 ||
                metadata.OccurredOn == default || priorOccurredAt is { } prior &&
                metadata.OccurredOn < prior || !TryApplyArtifactEvent(item.Event, tenantId,
                    artifactId, ref currentRegistration, ref currentState, ref currentReason,
                    metadata.OccurredOn))
                return InvalidArtifactHistory();

            if (metadata.OccurredOn <= through)
            {
                registration = currentRegistration;
                state = currentState;
                reason = currentReason;
                asOfRevision++;
                try
                {
                    asOfEvents.Add(Capture(item.Event, EvidenceStreams.Area, artifactId, metadata));
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException or
                    InvalidOperationException or NotSupportedException)
                {
                    return InvalidArtifactHistory();
                }
            }
            priorOccurredAt = metadata.OccurredOn;
        }

        if (currentRegistration is null || currentState is null)
            return InvalidArtifactHistory();
        var aggregate = await aggregates.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct)
            .ConfigureAwait(false);
        if (!aggregate.IsCreated || aggregate.CommittedStreamPosition != (ulong)first.Value.Count ||
            aggregate.Revision != first.Value.Count || aggregate.Registration != currentRegistration ||
            aggregate.State != currentState || aggregate.StateReason != currentReason)
            return InvalidArtifactHistory();

        return Result<ArtifactAsOf>.Success(new(registration, state, reason, asOfRevision,
            Array.AsReadOnly(asOfEvents.ToArray())));
    }

    static bool TryApplyArtifactEvent(DomainEvent domainEvent, Uuid tenantId, Uuid artifactId,
        ref EvidenceArtifactRegistered? registration, ref string? state, ref string? reason,
        DateTimeOffset occurredOn)
    {
        switch (domainEvent)
        {
            case EvidenceArtifactRegistered ev when registration is null && state is null &&
                ev.TenantId == tenantId && ev.ArtifactId == artifactId && ev.Content is not null &&
                IsDigest(ev.ContentSha256) && ev.ContentLength > 0 && ev.Collector is
                { Kind: "member", Id.Length: > 0, Display.Length: > 0 } &&
                Uuid.TryParse(ev.Collector.Id, out var collectorId) && collectorId != Uuid.Empty &&
                ev.RegisteredAt == occurredOn && ev.Content.CapturedAt <= occurredOn &&
                ev.Content.PeriodStart <= ev.Content.PeriodEnd:
                registration = ev;
                state = EvidenceArtifactStates.PendingInspection;
                reason = null;
                return true;
            case EvidenceArtifactInspected ev when registration is not null &&
                state == EvidenceArtifactStates.PendingInspection && ev.TenantId == tenantId &&
                ev.ArtifactId == artifactId && ev.InspectedAt == occurredOn && ValidInspection(ev):
                state = ev.State;
                reason = ev.Reason;
                return true;
            case EvidenceArtifactQuarantineReleased ev when registration is not null &&
                state == EvidenceArtifactStates.Quarantined && reason == EvidenceArtifactStates.Malware &&
                ev.TenantId == tenantId && ev.ArtifactId == artifactId && ev.RequestId != Uuid.Empty &&
                ev.Revision > 0 && ev.RescanState == "clean" && ev.RescannedAt <= ev.DecidedAt &&
                ev.DecidedAt == occurredOn && !string.IsNullOrWhiteSpace(ev.Rationale) &&
                ev.Rationale == ev.Rationale.Trim() && ev.DecidedBy is
                { Kind: "member", Id.Length: > 0, Display.Length: > 0 } &&
                Uuid.TryParse(ev.DecidedBy.Id, out var deciderId) && deciderId != Uuid.Empty:
                state = EvidenceArtifactStates.Available;
                reason = null;
                return true;
            case EvidenceArtifactDownloadIssued ev when registration is not null &&
                state == EvidenceArtifactStates.Available && reason is null &&
                ev.TenantId == tenantId && ev.ArtifactId == artifactId && ev.Revision > 0 &&
                ev.RequestId != Uuid.Empty && ev.ContentSha256 == registration.ContentSha256 &&
                ev.ContentLength == registration.ContentLength && !string.IsNullOrWhiteSpace(ev.Purpose) &&
                ev.Purpose == ev.Purpose.Trim() && ev.IssuedBy is
                { Kind: "member", Id.Length: > 0, Display.Length: > 0 } &&
                Uuid.TryParse(ev.IssuedBy.Id, out var issuerId) && issuerId != Uuid.Empty &&
                ev.IssuedAt >= registration.RegisteredAt && ev.IssuedAt == occurredOn:
                return true;
            default:
                return false;
        }
    }

    async ValueTask<Result<EvidenceHandlingPolicyView?>> ReadHandlingPolicyAsOfAsync(
        Uuid tenantId, Uuid artifactId, DateTimeOffset through, long artifactRevision,
        CancellationToken ct)
    {
        var policyId = EvidenceHandlingPolicy.IdFor(tenantId, artifactId);
        var stream = new EventStreamAddress(tenantId.ToString(), "evidence-handling-policies",
            policyId.ToString());
        var read = await ReadStreamTwiceAsync(stream, ct).ConfigureAwait(false);
        if (!read.IsSuccess)
            return Result<EvidenceHandlingPolicyView?>.Failure(read.Error);

        var states = new List<EvidenceHandlingPolicyView>(read.Value.Count);
        var eventIds = new HashSet<Uuid>();
        ulong expectedVersion = 1;
        DateTimeOffset? priorOccurredAt = null;
        foreach (var record in read.Value)
        {
            if (record.Stream != stream || record.Event?.Metadata is not { } metadata ||
                metadata.AggregateId != policyId || metadata.EventId == Uuid.Empty ||
                !eventIds.Add(metadata.EventId) || metadata.AggregateVersion != expectedVersion ||
                metadata.OccurredOn == default || priorOccurredAt is { } prior &&
                metadata.OccurredOn < prior || !TryReadHandlingPolicyEvent(record.Event,
                    tenantId, artifactId, expectedVersion, metadata.OccurredOn, states,
                    out var state))
                return InvalidHistory<EvidenceHandlingPolicyView?>();
            states.Add(state);
            expectedVersion++;
            priorOccurredAt = metadata.OccurredOn;
        }

        var policy = await aggregates.HydrateAsync(new EvidenceHandlingPolicy(tenantId,
            artifactId), ct).ConfigureAwait(false);
        var confirmation = await aggregates.HydrateAsync(new EvidenceHandlingPolicy(tenantId,
            artifactId), ct).ConfigureAwait(false);
        if (policy.CommittedStreamPosition != confirmation.CommittedStreamPosition ||
            policy.Revision != confirmation.Revision ||
            !SamePolicyState(policy.Current, confirmation.Current) ||
            policy.CommittedStreamPosition != (ulong)read.Value.Count ||
            !SamePolicyState(policy.Current, states.LastOrDefault()))
            return Result<EvidenceHandlingPolicyView?>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "Evidence handling policy changed during the Type I source read.", isTransient: true));

        var asOf = states.LastOrDefault(state => state.AssignedAt <= through);
        if (asOf is null)
            return Result<EvidenceHandlingPolicyView?>.Success(null);
        if (asOf.SourcePosition > (ulong)artifactRevision)
            return InvalidHistory<EvidenceHandlingPolicyView?>();
        return Result<EvidenceHandlingPolicyView?>.Success(asOf);
    }

    static bool TryReadHandlingPolicyEvent(DomainEvent domainEvent, Uuid tenantId,
        Uuid artifactId, ulong streamVersion, DateTimeOffset occurredAt,
        List<EvidenceHandlingPolicyView> priorStates,
        out EvidenceHandlingPolicyView state)
    {
        state = null!;
        long revision;
        Uuid? actorMemberId;
        DateTimeOffset eventAt;
        switch (domainEvent)
        {
            case ArtifactHandlingPolicyAssigned ev:
                if (streamVersion != 1 || ev.TenantId != tenantId || ev.SubjectId != artifactId)
                    return false;
                revision = ev.Revision;
                actorMemberId = ev.ActorMemberId;
                eventAt = ev.OccurredAt;
                state = ev.State;
                break;
            case ArtifactHandlingPolicyRevised ev:
                if (streamVersion <= 1 || ev.TenantId != tenantId || ev.SubjectId != artifactId)
                    return false;
                revision = ev.Revision;
                actorMemberId = ev.ActorMemberId;
                eventAt = ev.OccurredAt;
                state = ev.State;
                break;
            default:
                return false;
        }

        if (state is null || revision != (long)streamVersion || state.TenantId != tenantId ||
            state.ArtifactId != artifactId || state.Revision != revision ||
            state.AssignedAt != occurredAt || eventAt != occurredAt ||
            actorMemberId != (Uuid.TryParse(state.AssignedBy?.Id, out var memberId)
                ? memberId : null) || state.Source is null || state.Source.TenantId != tenantId ||
            state.Source.SourceKind != "evidence_artifact" || state.Source.SourceId != artifactId ||
            !IsDigest(state.Source.ContentSha256) || state.SourcePosition == 0 ||
            state.ContentLength <= 0 || state.History is null ||
            state.History.Count != revision || state.History.Count != priorStates.Count + 1 ||
            state.History.Take(priorStates.Count).Where((entry, index) =>
                !Equals(entry, priorStates[index].History[^1])).Any() ||
            state.History[^1] is not { } latest || latest.Revision != revision ||
            latest.AssignedAt != occurredAt || latest.AssignedBy != state.AssignedBy ||
            latest.HandlingClass != state.HandlingClass ||
            latest.PolicyVersion != state.PolicyVersion || latest.Rationale != state.Rationale)
            return false;
        return true;
    }

    static bool SamePolicyState(EvidenceHandlingPolicyView? first,
        EvidenceHandlingPolicyView? second) => first is null || second is null
        ? first is null && second is null
        : (first with { History = [] }).Equals(second with { History = [] }) &&
          first.History.SequenceEqual(second.History);

    async ValueTask<Result<RetentionAsOf>> ReadRetentionAsOfAsync(Uuid tenantId, Uuid artifactId,
        DateTimeOffset through, CancellationToken ct)
    {
        var loaded = await ArtifactRetentionSourceReader.LoadAsync(aggregates, tenantId,
            "evidence_artifact", artifactId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<RetentionAsOf>.Failure(loaded.Error!);
        var read = await new ArtifactRetentionReader(aggregates, events).ReadAsync(loaded.Value, ct).ConfigureAwait(false);
        if (!read.IsSuccess)
            return Result<RetentionAsOf>.Failure(read.Error!);
        var retention = read.Value;
        if (retention.Source is { } source && (source.TenantId != tenantId ||
                source.SourceKind != "evidence_artifact" || source.SourceId != artifactId ||
                !IsDigest(source.ContentSha256)))
            return InvalidHistory<RetentionAsOf>();

        var basis = retention.Basis is { RecordedAt: var recordedAt } value && recordedAt <= through
            ? value
            : null;
        var holds = retention.Holds.Where(hold => hold.PlacedAt <= through)
            .Select(hold => hold.ReleasedAt is { } releasedAt && releasedAt > through
                ? hold with { ReleaseReason = null, ReleasedBy = null, ReleasedAt = null }
                : hold)
            .OrderBy(static hold => hold.HoldId.ToString(), StringComparer.Ordinal).ToArray();
        return Result<RetentionAsOf>.Success(new(basis, Array.AsReadOnly(holds)));
    }

    async ValueTask<Result<RelatedGovernance>> ReadRelatedGovernanceAsync(
        ExaminationEngagementView engagement, IReadOnlySet<Uuid> artifactIds,
        DateTimeOffset through, CancellationToken ct)
    {
        var byArtifact = new Dictionary<Uuid, List<EvidenceGovernanceSourceEvent>>();
        var engagementHolds = new Dictionary<Uuid, List<EvidenceGovernanceSourceEvent>>();
        var areas = new[] { "artifact-disclosures", "evidence-redactions",
            "artifact-dispositions", "artifact-engagement-holds" };
        foreach (var area in areas)
        {
            var pattern = EventStreamPattern.ForPattern(engagement.TenantId.ToString(), area);
            var read = await ReadPatternTwiceAsync(pattern, ct).ConfigureAwait(false);
            if (!read.IsSuccess)
                return Result<RelatedGovernance>.Failure(read.Error);

            var versions = new Dictionary<Uuid, ulong>();
            var occurred = new Dictionary<Uuid, DateTimeOffset>();
            var eventIds = new HashSet<Uuid>();
            var redactionArtifacts = new Dictionary<Uuid, (Uuid Original, Uuid Derived)>();
            foreach (var record in read.Value)
            {
                if (record.Event?.Metadata is not { } metadata ||
                    record.Stream.Realm != engagement.TenantId.ToString() ||
                    record.Stream.Area != area ||
                    !Uuid.TryParse(record.Stream.Resource, null, out var aggregateId) ||
                    aggregateId == Uuid.Empty || metadata.AggregateId != aggregateId ||
                    metadata.EventId == Uuid.Empty || !eventIds.Add(metadata.EventId) ||
                    metadata.OccurredOn == default ||
                    metadata.AggregateVersion != versions.GetValueOrDefault(aggregateId) + 1 ||
                    occurred.TryGetValue(aggregateId, out var prior) && metadata.OccurredOn < prior ||
                    !TryRelatedEvent(record.Event, area, engagement.TenantId, aggregateId,
                        redactionArtifacts, out var businessAt, out var artifactReferences,
                        out var engagementId, out var isHoldEvent))
                    return InvalidRelatedGovernance();
                versions[aggregateId] = metadata.AggregateVersion;
                occurred[aggregateId] = metadata.OccurredOn;
                if (businessAt != metadata.OccurredOn)
                    return InvalidRelatedGovernance();
                if (businessAt > through || metadata.OccurredOn > through)
                    continue;

                EvidenceGovernanceSourceEvent sourceEvent;
                try
                {
                    sourceEvent = Capture(record.Event, area, aggregateId, metadata);
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException or
                    InvalidOperationException or NotSupportedException)
                {
                    return InvalidRelatedGovernance();
                }
                foreach (var artifactId in artifactReferences.Where(artifactIds.Contains).Distinct())
                {
                    if (!byArtifact.TryGetValue(artifactId, out var history))
                    {
                        history = [];
                        byArtifact.Add(artifactId, history);
                    }
                    history.Add(sourceEvent);
                }
                if (isHoldEvent && (engagementId == engagement.ExaminationEngagementId ||
                        artifactReferences.Any(artifactIds.Contains)))
                {
                    if (!engagementHolds.TryGetValue(aggregateId, out var history))
                    {
                        history = [];
                        engagementHolds.Add(aggregateId, history);
                    }
                    history.Add(sourceEvent);
                }
            }

            foreach (var (aggregateId, version) in versions)
            {
                var valid = await ValidateRelatedAggregateAsync(engagement.TenantId, area,
                    aggregateId, version, ct).ConfigureAwait(false);
                if (!valid)
                    return InvalidRelatedGovernance();
            }
        }

        return Result<RelatedGovernance>.Success(new(
            byArtifact.ToDictionary(static pair => pair.Key,
                static pair => (IReadOnlyList<EvidenceGovernanceSourceEvent>)Array.AsReadOnly(
                    pair.Value.OrderBy(static item => item.Area, StringComparer.Ordinal)
                        .ThenBy(static item => item.AggregateId.ToString(), StringComparer.Ordinal)
                        .ThenBy(static item => item.AggregateVersion).ToArray())),
            engagementHolds.ToDictionary(static pair => pair.Key,
                static pair => (IReadOnlyList<EvidenceGovernanceSourceEvent>)Array.AsReadOnly(
                    pair.Value.OrderBy(static item => item.AggregateVersion).ToArray()))));
    }

    async ValueTask<bool> ValidateRelatedAggregateAsync(Uuid tenantId, string area,
        Uuid aggregateId, ulong version, CancellationToken ct)
    {
        try
        {
            return area switch
            {
                "artifact-disclosures" => await ValidateAsync(new ArtifactDisclosure(tenantId,
                    aggregateId), version, static value => value.Revision).ConfigureAwait(false),
                "evidence-redactions" => await ValidateAsync(new EvidenceRedaction(tenantId,
                    aggregateId), version, static value => value.Revision).ConfigureAwait(false),
                "artifact-dispositions" => await ValidateAsync(new ArtifactDisposition(tenantId,
                    aggregateId), version, static value => value.Revision).ConfigureAwait(false),
                "artifact-engagement-holds" => await ValidateAsync(new ArtifactEngagementHold(
                    tenantId, aggregateId), version, static value => value.Revision).ConfigureAwait(false),
                _ => false,
            };
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        async ValueTask<bool> ValidateAsync<TAggregate>(TAggregate aggregate, ulong expectedVersion,
            Func<TAggregate, long> revision) where TAggregate : Aggregate
        {
            var hydrated = await aggregates.HydrateAsync(aggregate, ct).ConfigureAwait(false);
            return hydrated.Id == aggregateId && hydrated.CommittedStreamPosition == expectedVersion &&
                revision(hydrated) == (long)expectedVersion;
        }
    }

    static bool TryRelatedEvent(DomainEvent domainEvent, string area, Uuid tenantId,
        Uuid aggregateId, Dictionary<Uuid, (Uuid Original, Uuid Derived)> redactionArtifacts,
        out DateTimeOffset occurredAt, out IReadOnlyList<Uuid> artifactIds,
        out Uuid engagementId, out bool isHoldEvent)
    {
        occurredAt = default;
        artifactIds = [];
        engagementId = Uuid.Empty;
        isHoldEvent = false;
        switch (domainEvent)
        {
            case ArtifactDisclosureApproved ev when area == "artifact-disclosures" &&
                ev.TenantId == tenantId && ev.SubjectId == aggregateId && ev.State is { } disclosure:
                occurredAt = ev.OccurredAt;
                artifactIds = [disclosure.ArtifactId];
                return disclosure.Source?.SourceId == disclosure.ArtifactId &&
                    disclosure.Revision == ev.Revision && ev.Revision > 0;
            case ArtifactDisclosureDelivered ev when area == "artifact-disclosures" &&
                ev.TenantId == tenantId && ev.SubjectId == aggregateId && ev.State is { } delivery:
                occurredAt = ev.OccurredAt;
                artifactIds = [delivery.ArtifactId];
                return delivery.Source?.SourceId == delivery.ArtifactId &&
                    delivery.Revision == ev.Revision && ev.Revision > 1;
            case ArtifactDisclosureRevoked ev when area == "artifact-disclosures" &&
                ev.TenantId == tenantId && ev.SubjectId == aggregateId && ev.State is { } revoked:
                occurredAt = ev.OccurredAt;
                artifactIds = [revoked.ArtifactId];
                return revoked.Source?.SourceId == revoked.ArtifactId &&
                    revoked.Revision == ev.Revision && ev.Revision > 1;
            case EvidenceRedactionPrepared ev when area == "evidence-redactions" &&
                ev.TenantId == tenantId && ev.RedactionId == aggregateId &&
                ev.Preparation is { } preparation:
                occurredAt = preparation.PreparedAt;
                artifactIds = [preparation.Original.ArtifactId, preparation.Derived.ArtifactId];
                if (preparation.Original.TenantId != tenantId ||
                    preparation.Derived.TenantId != tenantId ||
                    preparation.Revision != ev.ExpectedRevision + 1)
                    return false;
                redactionArtifacts[aggregateId] = (preparation.Original.ArtifactId,
                    preparation.Derived.ArtifactId);
                return true;
            case EvidenceRedactionApproved ev when area == "evidence-redactions" &&
                ev.TenantId == tenantId && ev.RedactionId == aggregateId &&
                ev.Approval is { } approval && redactionArtifacts.TryGetValue(aggregateId,
                    out var artifacts):
                occurredAt = approval.ApprovedAt;
                artifactIds = [artifacts.Original, artifacts.Derived];
                return approval.Revision == ev.ExpectedRevision + 1;
            case ArtifactDispositionApproved ev when area == "artifact-dispositions" &&
                ev.TenantId == tenantId && ev.SubjectId == aggregateId && ev.State is { } approved:
                occurredAt = ev.OccurredAt;
                artifactIds = [approved.ArtifactId];
                return approved.Source?.SourceId == approved.ArtifactId &&
                    approved.Revision == ev.Revision && ev.Revision == 1;
            case ArtifactDispositionExecutionRequested ev when area == "artifact-dispositions" &&
                ev.TenantId == tenantId && ev.SubjectId == aggregateId && ev.State is { } requested:
                occurredAt = ev.OccurredAt;
                artifactIds = [requested.ArtifactId];
                return requested.Source?.SourceId == requested.ArtifactId &&
                    requested.Revision == ev.Revision && ev.Revision > 1;
            case ArtifactDispositionResultRecorded ev when area == "artifact-dispositions" &&
                ev.TenantId == tenantId && ev.SubjectId == aggregateId && ev.State is { } result:
                occurredAt = ev.OccurredAt;
                artifactIds = [result.ArtifactId];
                return result.Source?.SourceId == result.ArtifactId &&
                    result.Revision == ev.Revision && ev.Revision > 1;
            case ArtifactEngagementHoldPlaced ev when area == "artifact-engagement-holds" &&
                ev.TenantId == tenantId && ev.SubjectId == aggregateId && ev.State is { } placed:
                occurredAt = ev.OccurredAt;
                artifactIds = placed.ReportArtifactId is { } report ? [report] : [];
                engagementId = placed.EngagementId;
                isHoldEvent = true;
                return placed.HoldId == aggregateId && placed.Revision == ev.Revision &&
                    ev.Revision == 1;
            case EngagementReportIssued ev when area == "artifact-engagement-holds" &&
                ev.TenantId == tenantId && ev.SubjectId == aggregateId && ev.State is { } issued:
                occurredAt = ev.OccurredAt;
                artifactIds = issued.ReportArtifactId is { } issuedReport ? [issuedReport] : [];
                engagementId = issued.EngagementId;
                isHoldEvent = true;
                return issued.HoldId == aggregateId && issued.Revision == ev.Revision &&
                    ev.Revision > 1;
            case ArtifactEngagementHoldReleased ev when area == "artifact-engagement-holds" &&
                ev.TenantId == tenantId && ev.SubjectId == aggregateId && ev.State is { } released:
                occurredAt = ev.OccurredAt;
                artifactIds = released.ReportArtifactId is { } releasedReport ? [releasedReport] : [];
                engagementId = released.EngagementId;
                isHoldEvent = true;
                return released.HoldId == aggregateId && released.Revision == ev.Revision &&
                    ev.Revision > 1;
            default:
                return false;
        }
    }

    static Result<RelatedGovernance> InvalidRelatedGovernance() =>
        Result<RelatedGovernance>.Failure(new RequestError(RequestErrorKind.Conflict,
            "An evidence disclosure, redaction, disposition, or engagement-hold source is invalid."));

    async ValueTask<Result<IReadOnlyList<ReadRecord>>> ReadPatternTwiceAsync(
        EventStreamPattern pattern, CancellationToken ct)
    {
        var first = await ReadPatternAsync(pattern, ct).ConfigureAwait(false);
        if (!first.IsSuccess)
            return first;
        var second = await ReadPatternAsync(pattern, ct).ConfigureAwait(false);
        if (!second.IsSuccess)
            return second;
        return SameHistory(first.Value, second.Value)
            ? first
            : Result<IReadOnlyList<ReadRecord>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Evidence support changed during the Type I source read.", isTransient: true));
    }

    async ValueTask<Result<IReadOnlyList<ReadRecord>>> ReadStreamTwiceAsync(
        EventStreamAddress stream, CancellationToken ct)
    {
        var first = await ReadStreamAsync(stream, ct).ConfigureAwait(false);
        if (!first.IsSuccess)
            return first;
        var second = await ReadStreamAsync(stream, ct).ConfigureAwait(false);
        if (!second.IsSuccess)
            return second;
        return SameHistory(first.Value, second.Value)
            ? first
            : Result<IReadOnlyList<ReadRecord>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Evidence artifact history changed during the Type I source read.", isTransient: true));
    }

    async ValueTask<Result<IReadOnlyList<ReadRecord>>> ReadPatternAsync(
        EventStreamPattern pattern, CancellationToken ct)
    {
        var records = new List<ReadRecord>();
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct)
                           .ConfigureAwait(false))
        {
            if (records.Count >= MaximumEvents)
                return InvalidHistory<IReadOnlyList<ReadRecord>>();
            records.Add(new ReadRecord(record.Stream, record.Event, record.NextCursor));
        }
        return Result<IReadOnlyList<ReadRecord>>.Success(records.AsReadOnly());
    }

    async ValueTask<Result<IReadOnlyList<ReadRecord>>> ReadStreamAsync(
        EventStreamAddress stream, CancellationToken ct)
    {
        var records = new List<ReadRecord>();
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (records.Count >= MaximumEvents)
                return InvalidHistory<IReadOnlyList<ReadRecord>>();
            records.Add(new ReadRecord(record.Stream, record.Event, record.NextCursor));
        }
        return Result<IReadOnlyList<ReadRecord>>.Success(records.AsReadOnly());
    }

    static bool SameHistory(IReadOnlyList<ReadRecord> left, IReadOnlyList<ReadRecord> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Stream == pair.Second.Stream && pair.First.NextCursor == pair.Second.NextCursor &&
            pair.First.Event?.GetType() == pair.Second.Event?.GetType() &&
            pair.First.Event?.Metadata == pair.Second.Event?.Metadata &&
            Equals(pair.First.Event, pair.Second.Event));

    static bool ValidSupportRecord(ReadRecord record, Uuid tenantId)
    {
        if (record.Stream.Realm != tenantId.ToString() || record.Stream.Area != SupportArea ||
            !Uuid.TryParse(record.Stream.Resource, null, out var resourceId) || resourceId == Uuid.Empty ||
            record.Event is not EvidenceSupportLinked link || link.Metadata is not { } metadata ||
            metadata.EventId == Uuid.Empty || metadata.AggregateId != resourceId ||
            metadata.AggregateVersion != 1 || metadata.OccurredOn != link.OccurredAt ||
            link.TenantId != tenantId || link.SubjectId != resourceId || link.Revision != 1 ||
            link.ProgramId == Uuid.Empty || link.ArtifactId == Uuid.Empty ||
            link.ArtifactRevision <= 0 || !IsDigest(link.ArtifactContentSha256) ||
            link.SupportedRecordType is not ("control" or "control_version" or "control_occurrence" or
                "control_evaluation" or "period") || link.SupportedRecordId == Uuid.Empty ||
            link.SupportedRecordRevision <= 0 || link.PeriodStart == default ||
            link.PeriodEnd == default || link.PeriodStart > link.PeriodEnd ||
            string.IsNullOrWhiteSpace(link.Purpose) || link.Purpose != link.Purpose.Trim() ||
            string.IsNullOrWhiteSpace(link.SufficiencyRationale) ||
            link.SufficiencyRationale != link.SufficiencyRationale.Trim() ||
            link.ActorMemberId is not { } actorId || actorId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(link.ActorDisplay) || link.ActorDisplay != link.ActorDisplay.Trim())
            return false;
        return true;
    }

    static EvidenceGovernanceSourceEvent Capture(DomainEvent domainEvent, string area,
        Uuid aggregateId, DomainEventMetadata metadata)
    {
        var context = ComplianceCoreJsonContext.Default;
        var (kind, payload) = domainEvent switch
        {
            EvidenceSupportLinked ev => ("evidence_support_linked",
                JsonSerializer.SerializeToElement(ev, context.EvidenceSupportLinked)),
            EvidenceArtifactRegistered ev => ("evidence_artifact_registered",
                JsonSerializer.SerializeToElement(ev, context.EvidenceArtifactRegistered)),
            EvidenceArtifactInspected ev => ("evidence_artifact_inspected",
                JsonSerializer.SerializeToElement(ev, context.EvidenceArtifactInspected)),
            EvidenceArtifactQuarantineReleased ev => ("evidence_artifact_quarantine_released",
                JsonSerializer.SerializeToElement(ev, context.EvidenceArtifactQuarantineReleased)),
            EvidenceArtifactDownloadIssued ev => ("evidence_artifact_download_issued",
                JsonSerializer.SerializeToElement(ev, context.EvidenceArtifactDownloadIssued)),
            ArtifactDisclosureApproved ev => ("artifact_disclosure_approved",
                JsonSerializer.SerializeToElement(ev, context.ArtifactDisclosureApproved)),
            ArtifactDisclosureDelivered ev => ("artifact_disclosure_delivered",
                JsonSerializer.SerializeToElement(ev, context.ArtifactDisclosureDelivered)),
            ArtifactDisclosureRevoked ev => ("artifact_disclosure_revoked",
                JsonSerializer.SerializeToElement(ev, context.ArtifactDisclosureRevoked)),
            EvidenceRedactionPrepared ev => ("evidence_redaction_prepared",
                JsonSerializer.SerializeToElement(ev, context.EvidenceRedactionPrepared)),
            EvidenceRedactionApproved ev => ("evidence_redaction_approved",
                JsonSerializer.SerializeToElement(ev, context.EvidenceRedactionApproved)),
            ArtifactDispositionApproved ev => ("artifact_disposition_approved",
                JsonSerializer.SerializeToElement(ev, context.ArtifactDispositionApproved)),
            ArtifactDispositionExecutionRequested ev => ("artifact_disposition_execution_requested",
                JsonSerializer.SerializeToElement(ev, context.ArtifactDispositionExecutionRequested)),
            ArtifactDispositionResultRecorded ev => ("artifact_disposition_result_recorded",
                JsonSerializer.SerializeToElement(ev, context.ArtifactDispositionResultRecorded)),
            ArtifactEngagementHoldPlaced ev => ("artifact_engagement_hold_placed",
                JsonSerializer.SerializeToElement(ev, context.ArtifactEngagementHoldPlaced)),
            EngagementReportIssued ev => ("engagement_report_issued",
                JsonSerializer.SerializeToElement(ev, context.EngagementReportIssued)),
            ArtifactEngagementHoldReleased ev => ("artifact_engagement_hold_released",
                JsonSerializer.SerializeToElement(ev, context.ArtifactEngagementHoldReleased)),
            _ => throw new JsonException("An evidence stream contains an unknown event."),
        };
        return new EvidenceGovernanceSourceEvent(area, aggregateId, metadata.EventId,
            metadata.AggregateVersion, metadata.OccurredOn, kind, payload);
    }

    static string Digest(EvidenceGovernanceTypeIBaselineSourceContent content)
    {
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.EvidenceGovernanceTypeIBaselineSourceContent);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var domain = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[domain.Length + bytes.Length];
        domain.CopyTo(material, 0);
        bytes.CopyTo(material, domain.Length);
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }

    static string Digest(EvidenceGovernanceHoldSourceContent content)
    {
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.EvidenceGovernanceHoldSourceContent);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var domain = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[domain.Length + bytes.Length];
        domain.CopyTo(material, 0);
        bytes.CopyTo(material, domain.Length);
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }

    static bool ValidInspection(EvidenceArtifactInspected ev) => ev.State switch
    {
        EvidenceArtifactStates.Available => ev.Reason is null,
        EvidenceArtifactStates.Quarantined => ev.Reason == EvidenceArtifactStates.Malware,
        EvidenceArtifactStates.Rejected => ev.Reason is EvidenceArtifactStates.SecretDetected or
            EvidenceArtifactStates.Invalid,
        _ => false,
    };

    static bool IsDigest(string? value) => value is { Length: 64 } && value.All(static character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result<TypeIBaselineSourceContribution> InvalidHistory() =>
        InvalidHistory<TypeIBaselineSourceContribution>();

    static Result<ArtifactAsOf> InvalidArtifactHistory() => InvalidHistory<ArtifactAsOf>();

    static Result<T> InvalidHistory<T>() => Result<T>.Failure(new RequestError(
        RequestErrorKind.Conflict, "An evidence-governance source stream is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> Failure(RequestErrorKind kind, string message) =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(kind, message));

    sealed record ArtifactAsOf(EvidenceArtifactRegistered? Registration, string? State,
        string? StateReason, long AsOfRevision, IReadOnlyList<EvidenceGovernanceSourceEvent> Events);

    sealed record RetentionAsOf(ArtifactRetentionBasisRecorded? Basis,
        IReadOnlyList<ArtifactLegalHoldView> Holds);

    sealed record RelatedGovernance(
        IReadOnlyDictionary<Uuid, IReadOnlyList<EvidenceGovernanceSourceEvent>> ByArtifact,
        IReadOnlyDictionary<Uuid, IReadOnlyList<EvidenceGovernanceSourceEvent>> EngagementHolds);

    sealed record ReadRecord(EventStreamAddress Stream, DomainEvent? Event, EventCursor NextCursor);
}
