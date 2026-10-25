using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Snapshots;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures program risk and governance history at the Type I cutoff.</summary>
public sealed class RiskTypeIBaselineSourceContributor(
    IRiskDraftDirectoryReader drafts, IRiskEvaluationDirectoryReader evaluations,
    RiskGovernanceReader governanceReader, IImmutableContentStore content,
    IAggregateReader reader, IDomainEventReader events) : ITypeIBaselineSourceContributor
{
    const int PageSize = 200;
    const int MaximumEventsPerStream = 100_000;
    const string DigestDomain = "bdgrz.snapshot.source.risk.v1\0";

    public string Family => "risks";

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
            return Failure(RequestErrorKind.Validation,
                "A risk baseline read requires its engagement and source context.");
        var capturedControlVersions = new HashSet<(Uuid ControlId, Uuid VersionId)>();
        var capturedControlIds = new HashSet<Uuid>();
        foreach (var source in context.PreviouslyCapturedSources.Where(static source =>
                     source.Kind == "control_version"))
            if (source.RecordId == Uuid.Empty || source.VersionId == Uuid.Empty ||
                source.Revision < 1 || !IsSha256(source.ContentSha256) ||
                !capturedControlIds.Add(source.RecordId) ||
                !capturedControlVersions.Add((source.RecordId, source.VersionId)))
                return SourceConflict();

        var draftFence = await drafts.LoadCheckpointAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        var evaluationFence = await evaluations.LoadCheckpointAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        var draftPattern = EventStreamPattern.ForPattern(engagement.TenantId.ToString(), "risks");
        var evaluationPattern = EventStreamPattern.ForPattern(engagement.TenantId.ToString(),
            "risk-evaluations");
        if (await HasPendingSourceAsync(draftPattern, draftFence, ct).ConfigureAwait(false) ||
            await HasPendingSourceAsync(evaluationPattern, evaluationFence, ct).ConfigureAwait(false))
            return BehindSource();

        var listed = await ListRisksAsync(engagement, ct).ConfigureAwait(false);
        if (!listed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(listed.Error);
        var summaries = listed.Value;
        var knownRiskIds = summaries.Select(static summary => summary.RiskId).ToHashSet();
        var through = context.RecordedThrough ?? new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var seenEventIds = new HashSet<Uuid>();

        var methodHistory = await ReadMethodHistoryAsync(context, engagement.TenantId,
            engagement.ProgramId, through, seenEventIds, ct).ConfigureAwait(false);
        if (!methodHistory.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(methodHistory.Error);
        var governance = await ReadGovernanceHistoryAsync(context, engagement.TenantId,
            engagement.ProgramId, through, knownRiskIds, ct).ConfigureAwait(false);
        if (!governance.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(governance.Error);

        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var summary in summaries.OrderBy(static item => item.RiskId.ToString(),
                     StringComparer.Ordinal))
        {
            var draftHistory = await ReadDraftHistoryAsync(context, engagement.TenantId,
                engagement.ProgramId, summary, through, seenEventIds, ct).ConfigureAwait(false);
            if (!draftHistory.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(draftHistory.Error);
            if (draftHistory.Value.AsOfRevision == 0)
            {
                scopeDifferences.Add(
                    $"Risk {summary.Identifier} was not recorded on the Type I date.");
                continue;
            }

            var evaluationHistory = await ReadEvaluationHistoryAsync(context, engagement.TenantId,
                engagement.ProgramId, summary.RiskId, through, seenEventIds, ct)
                .ConfigureAwait(false);
            if (!evaluationHistory.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(evaluationHistory.Error);

            var governanceHistory = governance.Value.ForRisk(summary.RiskId);
            var eventsForRisk = new List<RiskSourceEvent>();
            eventsForRisk.AddRange(methodHistory.Value.AsOfEvents);
            eventsForRisk.AddRange(draftHistory.Value.AsOfEvents);
            eventsForRisk.AddRange(evaluationHistory.Value.AsOfEvents);
            eventsForRisk.AddRange(governanceHistory.AsOfEvents);
            eventsForRisk = eventsForRisk.OrderBy(static item => item.Area, StringComparer.Ordinal)
                .ThenBy(static item => item.AggregateId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.AggregateVersion).ToList();

            var content = new RiskSourceContent(engagement.PeriodStart, engagement.ProgramId,
                eventsForRisk.AsReadOnly());
            try
            {
                sources.Add(new TypeIBaselineSourceReference("risk", summary.RiskId,
                    Uuid.CreateVersion5(summary.RiskId,
                        "risk_v1:" + engagement.PeriodStart.ToString("yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture)),
                    draftHistory.Value.AsOfRevision, Digest(content)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                InvalidOperationException or NotSupportedException)
            {
                var difference = $"Risk {summary.Identifier} has invalid canonical source content.";
                blockers.Add(difference);
                scopeDifferences.Add(difference);
                continue;
            }

            AddReadinessBlockers(summary.Identifier, through,
                evaluationHistory.Value.AsOfState, governanceHistory, methodHistory.Value,
                capturedControlVersions, blockers);
        }

        if (!await ConfirmFenceAsync(engagement.TenantId, draftPattern, draftFence,
                drafts.LoadCheckpointAsync, ct).ConfigureAwait(false) ||
            !await ConfirmFenceAsync(engagement.TenantId, evaluationPattern, evaluationFence,
                evaluations.LoadCheckpointAsync, ct).ConfigureAwait(false))
            return BehindSource();

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(),
                blockers.AsReadOnly(), scopeDifferences.AsReadOnly()));
    }

    async ValueTask<Result<IReadOnlyList<RiskDraftView>>> ListRisksAsync(
        ExaminationEngagementView engagement, CancellationToken ct)
    {
        var summaries = new List<RiskDraftView>();
        var riskIds = new HashSet<Uuid>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await drafts.ListProgramAsync(engagement.TenantId, engagement.ProgramId,
                PageSize, cursor, ct).ConfigureAwait(false);
            if (page.Items is null || page.Items.Count > PageSize)
                return InvalidDirectory<IReadOnlyList<RiskDraftView>>();
            foreach (var summary in page.Items)
            {
                if (!ValidSummary(summary, engagement) || !riskIds.Add(summary.RiskId))
                    return InvalidDirectory<IReadOnlyList<RiskDraftView>>();
                summaries.Add(summary);
            }
            var next = page.NextCursor;
            if (next is not null && (next == cursor || !cursors.Add(next)))
                return InvalidDirectory<IReadOnlyList<RiskDraftView>>();
            cursor = next;
        } while (cursor is not null);
        return Result<IReadOnlyList<RiskDraftView>>.Success(summaries.AsReadOnly());
    }

    async ValueTask<Result<DraftHistory>> ReadDraftHistoryAsync(TypeIBaselineSourceReadContext context, Uuid tenantId, Uuid programId,
        RiskDraftView summary, DateTimeOffset through, HashSet<Uuid> seenEventIds,
        CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), "risks",
            summary.RiskId.ToString());
        var asOfEvents = new List<RiskSourceEvent>();
        var created = (RiskDraftCreated?)null;
        RiskDraftContent? currentContent = null;
        DateTimeOffset currentChangedAt = default;
        var currentChangedByMemberId = Uuid.Empty;
        string? currentChangedByDisplay = null;
        var currentRevision = 0L;
        var asOfRevision = 0L;
        var expectedVersion = 1UL;
        DateTimeOffset? priorRecordedAt = null;
        var excluded = false;
        var count = 0;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (++count > MaximumEventsPerStream || record.Stream != stream ||
                record.Event?.Metadata is not { } metadata ||
                !ValidMetadata(metadata, summary.RiskId, expectedVersion, seenEventIds) ||
                context.RecordedThrough is null && priorRecordedAt is { } prior && metadata.OccurredOn < prior)
                return InvalidHistory<DraftHistory>();
            var domainEvent = record.Event;
            DateTimeOffset occurredAt;
            switch (domainEvent)
            {
                case RiskDraftCreated ev when created is null && currentRevision == 0 &&
                    ev.TenantId == tenantId && ev.ProgramId == programId && ev.RiskId == summary.RiskId &&
                    ev.CreateRequestId != Uuid.Empty &&
                    ev.Identifier == RiskDraft.NormalizeIdentifier(ev.Identifier) &&
                    RiskDraft.IdFor(tenantId, programId, ev.Identifier) == summary.RiskId &&
                    ValidDraftContent(ev.Content) && ev.ActorMemberId != Uuid.Empty &&
                    ValidText(ev.ActorDisplay, 200):
                    created = ev;
                    currentContent = ev.Content;
                    currentChangedAt = ev.ChangedAt;
                    currentChangedByMemberId = ev.ActorMemberId;
                    currentChangedByDisplay = ev.ActorDisplay;
                    currentRevision = 1;
                    occurredAt = ev.ChangedAt;
                    break;
                case RiskDraftRevised ev when created is not null &&
                    ev.TenantId == tenantId && ev.ProgramId == programId &&
                    ev.RiskId == summary.RiskId && ev.Revision == currentRevision + 1 &&
                    ValidDraftContent(ev.Content) && ev.ActorMemberId != Uuid.Empty &&
                    ValidText(ev.ActorDisplay, 200):
                    currentContent = ev.Content;
                    currentChangedAt = ev.ChangedAt;
                    currentChangedByMemberId = ev.ActorMemberId;
                    currentChangedByDisplay = ev.ActorDisplay;
                    currentRevision = ev.Revision;
                    occurredAt = ev.ChangedAt;
                    break;
                default:
                    return InvalidHistory<DraftHistory>();
            }
            if (occurredAt == default || metadata.OccurredOn == default)
                return InvalidHistory<DraftHistory>();
            var isAsOf = context.RecordedThrough is null ? occurredAt <= through && metadata.OccurredOn <= through :
                await context.IncludesAsync(record.Stream, record.Event, ct).ConfigureAwait(false);
            if (isAsOf && excluded)
                return InvalidHistory<DraftHistory>();
            if (isAsOf)
            {
                if (asOfRevision + 1 != currentRevision)
                    return InvalidHistory<DraftHistory>();
                asOfRevision = currentRevision;
                asOfEvents.Add(SerializeEvent(domainEvent, "risks", summary.RiskId, metadata));
            }
            else
            {
                excluded = true;
            }
            priorRecordedAt = metadata.OccurredOn;
            expectedVersion++;
        }

        var current = await reader.HydrateAsync(new RiskDraft(tenantId, summary.RiskId), ct)
            .ConfigureAwait(false);
        if (created is null || currentRevision != summary.Revision || !current.IsCreated ||
            current.Id != summary.RiskId || current.ProgramId != programId ||
            current.Revision != currentRevision || summary.Content != currentContent ||
            summary.LastChangedAt != currentChangedAt ||
            summary.LastChangedByMemberId != currentChangedByMemberId ||
            summary.LastChangedByDisplay != currentChangedByDisplay)
            return InvalidHistory<DraftHistory>();

        return Result<DraftHistory>.Success(new DraftHistory(asOfRevision, asOfEvents));
    }

    async ValueTask<Result<EvaluationHistory>> ReadEvaluationHistoryAsync(TypeIBaselineSourceReadContext context, Uuid tenantId,
        Uuid programId, Uuid riskId, DateTimeOffset through, HashSet<Uuid> seenEventIds,
        CancellationToken ct)
    {
        var stream = new EventStreamAddress(tenantId.ToString(), "risk-evaluations",
            riskId.ToString());
        var asOfEvents = new List<RiskSourceEvent>();
        var currentState = new RiskEvaluationState();
        var asOfState = new RiskEvaluationState();
        var expectedVersion = 1UL;
        var expectedRevision = 1L;
        DateTimeOffset? priorRecordedAt = null;
        var excluded = false;
        var count = 0;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (++count > MaximumEventsPerStream || record.Stream != stream ||
                record.Event?.Metadata is not { } metadata ||
                !ValidMetadata(metadata, riskId, expectedVersion, seenEventIds) ||
                context.RecordedThrough is null && priorRecordedAt is { } prior && metadata.OccurredOn < prior ||
                !TryEvaluationEvent(record.Event, tenantId, programId, riskId,
                    expectedRevision, out var occurredAt))
                return InvalidHistory<EvaluationHistory>();
            if (!currentState.Apply(record.Event!))
                return InvalidHistory<EvaluationHistory>();
            var isAsOf = context.RecordedThrough is null ? occurredAt <= through && metadata.OccurredOn <= through :
                await context.IncludesAsync(record.Stream, record.Event, ct).ConfigureAwait(false);
            if (isAsOf && excluded)
                return InvalidHistory<EvaluationHistory>();
            if (isAsOf)
            {
                if (!asOfState.Apply(record.Event!))
                    return InvalidHistory<EvaluationHistory>();
                asOfEvents.Add(SerializeEvent(record.Event!, "risk-evaluations", riskId,
                    metadata));
            }
            else
            {
                excluded = true;
            }
            expectedRevision++;
            expectedVersion++;
            priorRecordedAt = metadata.OccurredOn;
        }

        var source = await reader.HydrateAsync(new RiskEvaluation(tenantId, riskId), ct)
            .ConfigureAwait(false);
        if (source.Revision != expectedRevision - 1 ||
            source.Revision > 0 && source.ProgramId != programId ||
            !SameJson(source.ToView(), currentState.ToView(tenantId, source.ProgramId, riskId,
                expectedRevision - 1), ComplianceCoreJsonContext.Default.RiskEvaluationView))
            return InvalidHistory<EvaluationHistory>();
        var projected = await evaluations.GetAsync(tenantId, riskId, ct).ConfigureAwait(false);
        if (source.Revision == 0 ? projected is not null && projected.Revision != 0 :
            projected is null || projected.TenantId != tenantId ||
            projected.ProgramId != programId || projected.RiskId != riskId ||
            projected.Revision != source.Revision ||
            !SameJson(projected, source.ToView(),
                ComplianceCoreJsonContext.Default.RiskEvaluationView))
            return InvalidHistory<EvaluationHistory>();

        return Result<EvaluationHistory>.Success(new EvaluationHistory(source.Revision,
            asOfEvents, asOfState));
    }

    async ValueTask<Result<MethodHistory>> ReadMethodHistoryAsync(TypeIBaselineSourceReadContext context, Uuid tenantId, Uuid programId,
        DateTimeOffset through, HashSet<Uuid> seenEventIds, CancellationToken ct)
    {
        var methodId = RiskMethod.IdFor(tenantId, programId);
        var stream = new EventStreamAddress(tenantId.ToString(), "risk-methods",
            methodId.ToString());
        var versions = new List<RiskMethodVersionView>();
        var asOfVersions = new List<RiskMethodVersionView>();
        var asOfEvents = new List<RiskSourceEvent>();
        var expectedVersion = 1UL;
        DateTimeOffset? priorRecordedAt = null;
        var excluded = false;
        var count = 0;
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (++count > MaximumEventsPerStream || record.Stream != stream ||
                record.Event?.Metadata is not { } metadata ||
                !ValidMetadata(metadata, methodId, expectedVersion, seenEventIds) ||
                context.RecordedThrough is null && priorRecordedAt is { } prior && metadata.OccurredOn < prior ||
                record.Event is not RiskMethodVersionPublished published ||
                !TryMethodEvent(published, tenantId, programId, versions.Count + 1,
                    out var occurredAt))
                return InvalidHistory<MethodHistory>();
            versions.Add(published.Version);
            var isAsOf = context.RecordedThrough is null ? occurredAt <= through && metadata.OccurredOn <= through :
                await context.IncludesAsync(record.Stream, record.Event, ct).ConfigureAwait(false);
            if (isAsOf && excluded)
                return InvalidHistory<MethodHistory>();
            if (isAsOf)
            {
                asOfVersions.Add(published.Version);
                asOfEvents.Add(SerializeEvent(published, "risk-methods", methodId, metadata));
            }
            else
                excluded = true;
            priorRecordedAt = metadata.OccurredOn;
            expectedVersion++;
        }

        var source = await reader.HydrateAsync(new RiskMethod(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (source.Id != methodId || source.PublishedVersion != versions.Count)
            return InvalidHistory<MethodHistory>();
        for (var index = 0; index < versions.Count; index++)
            if (!SameJson(source.GetVersion(index + 1), versions[index],
                    ComplianceCoreJsonContext.Default.RiskMethodVersionView))
                return InvalidHistory<MethodHistory>();

        return Result<MethodHistory>.Success(new MethodHistory(asOfVersions.AsReadOnly(),
            asOfEvents.AsReadOnly()));
    }

    async ValueTask<Result<GovernanceHistory>> ReadGovernanceHistoryAsync(TypeIBaselineSourceReadContext context, Uuid tenantId, Uuid programId,
        DateTimeOffset through, HashSet<Uuid> knownRiskIds, CancellationToken ct)
    {
        var discovered = await governanceReader.DiscoverAsync(tenantId, programId, null, ct).ConfigureAwait(false);
        var histories = new Dictionary<Uuid, GovernanceRiskHistory>();
        try
        {
            var owned = discovered.Governed.Concat(discovered.Lines.Select(static line => line.Risk))
                .Concat(discovered.Actions.Select(static action => action.Risk)).Distinct();
            if (owned.Any(riskId => !knownRiskIds.Contains(riskId)))
                return InvalidHistory<GovernanceHistory>();
            foreach (var riskId in owned)
            {
                var history = new GovernanceRiskHistory();
                histories.Add(riskId, history);
                var governance = await reader.HydrateAsync(new RiskGovernance(tenantId, programId, riskId), ct)
                    .ConfigureAwait(false);
                if (!await history.CaptureAsync(governance.Stream, governance.Facts, tenantId, content, through, context, ct)
                        .ConfigureAwait(false))
                    return InvalidHistory<GovernanceHistory>();
                foreach (var (_, controlId) in discovered.Lines.Where(line => line.Risk == riskId))
                {
                    var line = await reader.HydrateAsync(new RiskTreatmentAssertion(tenantId, programId, riskId,
                        controlId), ct).ConfigureAwait(false);
                    if (!await history.CaptureAsync(line.Stream, line.Facts, tenantId, content, through, context, ct)
                            .ConfigureAwait(false))
                        return InvalidHistory<GovernanceHistory>();
                }

                foreach (var (_, actionId) in discovered.Actions.Where(action => action.Risk == riskId))
                {
                    var action = await reader.HydrateAsync(new RiskTreatmentAction(tenantId, programId, riskId,
                        actionId), ct).ConfigureAwait(false);
                    if (!await history.CaptureAsync(action.Stream, action.Facts, tenantId, content, through, context, ct)
                            .ConfigureAwait(false))
                        return InvalidHistory<GovernanceHistory>();
                }
            }
        }
        catch (ImmutableContentIntegrityException)
        {
            return InvalidHistory<GovernanceHistory>();
        }

        return Result<GovernanceHistory>.Success(new GovernanceHistory(histories));
    }

    static bool TryEvaluationEvent(DomainEvent domainEvent, Uuid tenantId, Uuid programId,
        Uuid riskId, long expectedRevision, out DateTimeOffset occurredAt)
    {
        switch (domainEvent)
        {
            case RiskAssessmentRecorded { Assessment: { } assessment } recorded:
                occurredAt = assessment.AssessedAt;
                return recorded.TenantId == tenantId && recorded.ProgramId == programId &&
                    recorded.RiskId == riskId && recorded.Revision == expectedRevision &&
                    assessment.AssessmentId != Uuid.Empty &&
                    assessment.Phase is RiskEvaluation.Inherent or RiskEvaluation.Target or
                        RiskEvaluation.Residual && assessment.MethodVersionId != Uuid.Empty &&
                    assessment.MethodVersion >= 1 && assessment.Likelihood is >= 1 and <= 5 &&
                    assessment.Impact is >= 1 and <= 5 &&
                    assessment.Score == assessment.Likelihood * assessment.Impact &&
                    ValidText(assessment.Rationale, 4000) && ValidMemberActor(assessment.Assessor) &&
                    occurredAt != default;
            case RiskTreatmentChosen { Treatment: { } treatment } chosen:
                occurredAt = treatment.ChosenAt;
                return chosen.TenantId == tenantId && chosen.ProgramId == programId &&
                    chosen.RiskId == riskId && chosen.Revision == expectedRevision &&
                    treatment.Kind is "mitigate" or "accept" or "transfer" or "avoid" &&
                    ValidText(treatment.Rationale, 4000) && ValidMemberActor(treatment.ChosenBy) &&
                    occurredAt != default;
            case RiskAccepted { Acceptance: { } acceptance } accepted:
                occurredAt = acceptance.AcceptedAt;
                return accepted.TenantId == tenantId && accepted.ProgramId == programId &&
                    accepted.RiskId == riskId && accepted.Revision == expectedRevision &&
                    acceptance.AcceptanceId != Uuid.Empty &&
                    acceptance.ResidualAssessmentId != Uuid.Empty &&
                    acceptance.ResidualScore is >= 1 and <= 25 &&
                    acceptance.AppetiteThreshold is null or >= 1 and <= 25 &&
                    acceptance.ApproverMemberId != Uuid.Empty &&
                    ValidMemberActor(acceptance.Approver, acceptance.ApproverMemberId) &&
                    acceptance.ApproverAuthority is RiskEvaluation.ComplianceLead or
                        RiskEvaluation.Executive && ValidText(acceptance.Rationale, 4000) &&
                    acceptance.ExpiresAt > occurredAt &&
                    acceptance.ExpiresAt <= occurredAt.AddMonths(12) && occurredAt != default;
            default:
                occurredAt = default;
                return false;
        }
    }

    static void AddReadinessBlockers(string identifier, DateTimeOffset through,
        RiskEvaluationState evaluation, GovernanceRiskHistory governance, MethodHistory methods,
        HashSet<(Uuid ControlId, Uuid VersionId)> capturedControlVersions, List<string> blockers)
    {
        if (!evaluation.Assessments.Any(static assessment =>
                assessment.Phase == RiskEvaluation.Inherent))
            blockers.Add($"Risk {identifier} has no inherent assessment on the Type I date.");
        if (evaluation.Treatment is null)
            blockers.Add($"Risk {identifier} has no selected treatment on the Type I date.");
        if (governance.Owner is null)
            blockers.Add($"Risk {identifier} has no accountable owner on the Type I date.");

        foreach (var assessment in evaluation.Assessments)
            if (!methods.AsOfVersions.Any(method => method.MethodVersionId ==
                    assessment.MethodVersionId && method.Version == assessment.MethodVersion))
                blockers.Add($"Risk {identifier} references a method version unavailable on the Type I date.");

        foreach (var trigger in governance.Triggers)
            if (!evaluation.Assessments.Any(assessment =>
                    assessment.Phase == RiskEvaluation.Inherent &&
                    assessment.AssessedAt >= trigger.RaisedAt))
                blockers.Add($"Risk {identifier} has an unresolved reassessment trigger from {trigger.SourceReference}.");

        foreach (var treatment in governance.ControlTreatments.Where(static treatment =>
                     treatment.Status == "accepted"))
            if (!capturedControlVersions.Contains((treatment.ControlId,
                    treatment.ControlVersionId)))
                blockers.Add($"Risk {identifier} has an accepted control treatment that does not match a captured control version.");

        if (evaluation.Treatment is { Kind: "accept" })
        {
            var residual = evaluation.Assessments.LastOrDefault(static item =>
                item.Phase == RiskEvaluation.Residual);
            var acceptance = evaluation.Acceptances.LastOrDefault(item =>
                item.ResidualAssessmentId == residual?.AssessmentId && item.AcceptedAt <= through);
            if (acceptance is null || acceptance.ExpiresAt <= through)
                blockers.Add($"Risk {identifier} has no active acceptance for its current residual assessment on the Type I date.");
        }
    }

    static bool TryMethodEvent(RiskMethodVersionPublished published, Uuid tenantId,
        Uuid programId, int expectedVersion, out DateTimeOffset occurredAt)
    {
        var version = published.Version;
        occurredAt = version?.PublishedAt ?? default;
        return published.TenantId == tenantId && published.ProgramId == programId &&
            ValidMethodVersion(version, tenantId, programId, expectedVersion) &&
            occurredAt != default;
    }

    static bool ValidMethodVersion(RiskMethodVersionView? version, Uuid tenantId,
        Uuid programId, int expectedVersion) => version is not null &&
        version.TenantId == tenantId && version.ProgramId == programId &&
        version.MethodVersionId == Uuid.CreateVersion5(RiskMethod.IdFor(tenantId, programId),
            expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)) &&
        version.Version == expectedVersion && version.Kind == RiskMethod.QualitativeFiveByFive &&
        version.LikelihoodScale is { Count: 5 } &&
        version.LikelihoodScale.All(static item => ValidText(item, 200)) &&
        version.ImpactScale is { Count: 5 } &&
        version.ImpactScale.All(static item => ValidText(item, 200)) &&
        version.AppetiteThreshold is null or >= 1 and <= 25 &&
        version.ReassessmentInterval == RiskMethod.AnnualReassessment &&
        ValidMemberActor(version.PublishedBy) && version.PublishedAt != default;

    static bool ValidSummary(RiskDraftView? summary, ExaminationEngagementView engagement) =>
        summary is not null && summary.TenantId == engagement.TenantId &&
        summary.ProgramId == engagement.ProgramId && summary.RiskId != Uuid.Empty &&
        summary.Identifier == RiskDraft.NormalizeIdentifier(summary.Identifier) &&
        summary.Revision > 0 && summary.Status == "draft_unassessed" &&
        summary.OwnerResolution == "unresolved" && ValidDraftContent(summary.Content) &&
        summary.LastChangedByMemberId != Uuid.Empty &&
        ValidText(summary.LastChangedByDisplay, 200) && summary.LastChangedAt != default &&
        ValidMemberActor(summary.LastChangedBy, summary.LastChangedByMemberId) &&
        RiskDraft.IdFor(engagement.TenantId, engagement.ProgramId, summary.Identifier) ==
            summary.RiskId;

    static bool ValidDraftContent(RiskDraftContent? content) => content is not null &&
        ValidText(content.Title, 200) && ValidText(content.Scenario, 8000) &&
        ValidText(content.PotentialEffect, 8000) &&
        (content.SourceNote is null || ValidText(content.SourceNote, 4000));

    static bool ValidMetadata(DomainEventMetadata metadata, Uuid aggregateId,
        ulong expectedVersion, HashSet<Uuid> eventIds) => metadata.EventId != Uuid.Empty &&
        eventIds.Add(metadata.EventId) && metadata.AggregateId == aggregateId &&
        metadata.AggregateVersion == expectedVersion && metadata.OccurredOn != default;

    static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool ValidMemberActor(ActorReference? actor) => actor is { Kind: "member" } &&
        Uuid.TryParse(actor.Id, null, out var id) && id != Uuid.Empty &&
        ValidText(actor.Display, 200);

    static bool ValidMemberActor(ActorReference? actor, Uuid memberId) =>
        memberId != Uuid.Empty && ValidMemberActor(actor) && actor!.Id == memberId.ToString();

    static bool ValidText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && value == value.Trim();

    static bool SameJson<T>(T? left, T? right,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        if (left is null || right is null)
            return left is null && right is null;
        var leftElement = JsonSerializer.SerializeToElement(left, typeInfo);
        var rightElement = JsonSerializer.SerializeToElement(right, typeInfo);
        using var leftStream = new MemoryStream();
        using var rightStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(leftStream,
                   SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, leftElement);
        using (var writer = new Utf8JsonWriter(rightStream,
                   SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, rightElement);
        return leftStream.ToArray().AsSpan().SequenceEqual(rightStream.ToArray());
    }

    static RiskSourceEvent SerializeEvent(DomainEvent domainEvent, string area,
        Uuid aggregateId, DomainEventMetadata metadata)
    {
        var json = ComplianceCoreJsonContext.Default;
        var (kind, content) = domainEvent switch
        {
            RiskDraftCreated ev => ("draft_created", JsonSerializer.SerializeToElement(ev,
                json.RiskDraftCreated)),
            RiskDraftRevised ev => ("draft_revised", JsonSerializer.SerializeToElement(ev,
                json.RiskDraftRevised)),
            RiskAssessmentRecorded ev => ("assessment_recorded",
                JsonSerializer.SerializeToElement(ev, json.RiskAssessmentRecorded)),
            RiskTreatmentChosen ev => ("treatment_chosen", JsonSerializer.SerializeToElement(ev,
                json.RiskTreatmentChosen)),
            RiskAccepted ev => ("risk_accepted", JsonSerializer.SerializeToElement(ev,
                json.RiskAccepted)),
            RiskMethodVersionPublished ev => ("method_version_published",
                JsonSerializer.SerializeToElement(ev, json.RiskMethodVersionPublished)),
            RiskOwnerAssigned ev => ("owner_assigned", JsonSerializer.SerializeToElement(ev,
                json.RiskOwnerAssigned)),
            RiskControlTreatmentProposed ev => ("control_treatment_proposed",
                JsonSerializer.SerializeToElement(ev, json.RiskControlTreatmentProposed)),
            RiskControlTreatmentReviewed ev => ("control_treatment_reviewed",
                JsonSerializer.SerializeToElement(ev, json.RiskControlTreatmentReviewed)),
            RiskControlTreatmentRetired ev => ("control_treatment_retired",
                JsonSerializer.SerializeToElement(ev, json.RiskControlTreatmentRetired)),
            RiskReassessmentTriggered ev => ("reassessment_triggered",
                JsonSerializer.SerializeToElement(ev, json.RiskReassessmentTriggered)),
            RiskTreatmentActionAdded ev => ("treatment_action_added",
                JsonSerializer.SerializeToElement(ev, json.RiskTreatmentActionAdded)),
            RiskTreatmentActionCancelled ev => ("treatment_action_cancelled",
                JsonSerializer.SerializeToElement(ev, json.RiskTreatmentActionCancelled)),
            RiskTreatmentActionRevised ev => ("treatment_action_revised",
                JsonSerializer.SerializeToElement(ev, json.RiskTreatmentActionRevised)),
            RiskTreatmentActionCompletionSubmitted ev => ("treatment_action_completion_submitted",
                JsonSerializer.SerializeToElement(ev, json.RiskTreatmentActionCompletionSubmitted)),
            RiskTreatmentActionCompletionReviewed ev => ("treatment_action_completion_reviewed",
                JsonSerializer.SerializeToElement(ev, json.RiskTreatmentActionCompletionReviewed)),
            RiskTreatmentActionCompletionReviewAccepted ev => ("treatment_action_completion_review_accepted",
                JsonSerializer.SerializeToElement(ev, json.RiskTreatmentActionCompletionReviewAccepted)),
            RiskTreatmentActionCompletionReviewCanceled ev => ("treatment_action_completion_review_canceled",
                JsonSerializer.SerializeToElement(ev, json.RiskTreatmentActionCompletionReviewCanceled)),
            _ => throw new JsonException("The risk source stream contains an unknown event."),
        };
        return new RiskSourceEvent(area, aggregateId, metadata.EventId,
            metadata.AggregateVersion, metadata.OccurredOn, kind, content);
    }

    async ValueTask<bool> HasPendingSourceAsync(EventStreamPattern pattern,
        ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct)
            .GetAsyncEnumerator(ct);
        return await pending.MoveNextAsync().ConfigureAwait(false);
    }

    async ValueTask<bool> ConfirmFenceAsync(Uuid tenantId, EventStreamPattern pattern,
        ProjectionCheckpoint fence,
        Func<Uuid, CancellationToken, ValueTask<ProjectionCheckpoint>> loadCheckpoint,
        CancellationToken ct)
    {
        var current = await loadCheckpoint(tenantId, ct).ConfigureAwait(false);
        return current == fence &&
            !await HasPendingSourceAsync(pattern, current, ct).ConfigureAwait(false);
    }

    static string Digest(RiskSourceContent content)
    {
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.RiskSourceContent);
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

    static Result<T> InvalidDirectory<T>() => Result<T>.Failure(new RequestError(
        RequestErrorKind.Conflict, "The risk directory is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Failure(RequestErrorKind.Conflict,
            "A risk source or its exact control-version references are invalid or inconsistent.");

    static Result<T> InvalidHistory<T>() => Result<T>.Failure(new RequestError(
        RequestErrorKind.Conflict, "A risk source stream is invalid or inconsistent."));

    static Result<TypeIBaselineSourceContribution> BehindSource() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
            RequestErrorKind.Conflict,
            "A risk source projection changed or has not reached its event stream. Retry the query.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> Failure(RequestErrorKind kind,
        string message) => Result<TypeIBaselineSourceContribution>.Failure(new RequestError(kind,
        message));

    sealed record DraftHistory(long AsOfRevision, IReadOnlyList<RiskSourceEvent> AsOfEvents);

    sealed record EvaluationHistory(long CurrentRevision,
        IReadOnlyList<RiskSourceEvent> AsOfEvents, RiskEvaluationState AsOfState);

    sealed record MethodHistory(IReadOnlyList<RiskMethodVersionView> AsOfVersions,
        IReadOnlyList<RiskSourceEvent> AsOfEvents);

    sealed record GovernanceHistory(Dictionary<Uuid, GovernanceRiskHistory> Histories)
    {
        public GovernanceRiskHistory ForRisk(Uuid riskId) =>
            Histories.GetValueOrDefault(riskId) ?? new GovernanceRiskHistory();
    }

    sealed class GovernanceRiskHistory
    {
        public RiskOwnerView? Owner { get; private set; }
        public List<RiskControlTreatmentView> ControlTreatments { get; } = [];
        public List<RiskReassessmentTriggerView> Triggers { get; } = [];
        public List<RiskSourceEvent> AsOfEvents { get; } = [];

        /// <summary>
        ///     Folds the facts of one governance, assertion line or action stream that are inside the cutoff. The
        ///     inside facts must be a complete prefix of the stream; a fact after the cutoff followed by one inside is
        ///     an invalid history.
        /// </summary>
        public async ValueTask<bool> CaptureAsync(EventStreamAddress stream, IReadOnlyList<DomainEvent> facts,
            Uuid tenantId, IImmutableContentStore content, DateTimeOffset through, TypeIBaselineSourceReadContext context, CancellationToken ct)
        {
            var excluded = false;
            foreach (var fact in facts)
            {
                if (fact is not INativeOperationFact native || fact.Metadata is not { } metadata)
                    return false;
                var inside = context.RecordedThrough is null ? native.Operation.AcceptedAt <= through && metadata.OccurredOn <= through :
                    await context.IncludesAsync(stream, fact, ct).ConfigureAwait(false);
                if (!inside)
                {
                    excluded = true;
                    continue;
                }

                if (excluded)
                    return false;
                AsOfEvents.Add(SerializeEvent(fact, stream.Area, metadata.AggregateId, metadata));
                if (!await ApplyAsync(fact, tenantId, content, ct).ConfigureAwait(false))
                    return false;
            }

            return true;
        }

        async ValueTask<bool> ApplyAsync(DomainEvent domainEvent, Uuid tenantId, IImmutableContentStore store,
            CancellationToken ct)
        {
            switch (domainEvent)
            {
                case RiskOwnerAssigned assigned:
                    Owner = await FindingContent.ReadAsync<RiskOwnerView>(store, tenantId, assigned.OwnerContent, ct)
                        .ConfigureAwait(false);
                    return true;
                case RiskControlTreatmentProposed proposed:
                    var treatment = await FindingContent.ReadAsync<RiskControlTreatmentView>(store, tenantId,
                        proposed.TreatmentContent, ct).ConfigureAwait(false);
                    if (ControlTreatments.Any(item => item.TreatmentId == treatment.TreatmentId))
                        return false;
                    ControlTreatments.Add(treatment);
                    return true;
                case RiskControlTreatmentReviewed reviewed:
                    var reviewIndex = ControlTreatments.FindIndex(item =>
                        item.TreatmentId == reviewed.TreatmentId && item.Status == "proposed");
                    if (reviewIndex < 0)
                        return false;
                    var current = ControlTreatments[reviewIndex];
                    if (reviewed.Outcome == "accept")
                        for (var index = 0; index < ControlTreatments.Count; index++)
                            if (index != reviewIndex && ControlTreatments[index] is
                                { Status: "accepted" } prior && prior.ControlId == current.ControlId)
                                ControlTreatments[index] = prior with { Status = "superseded" };
                    ControlTreatments[reviewIndex] = current with
                    {
                        Status = reviewed.Outcome == "accept" ? "accepted" : "rejected",
                        ReviewDecisionId = reviewed.DecisionId,
                        ReviewedBy = reviewed.Reviewer,
                        ReviewRationale = reviewed.Rationale,
                        ReviewedAt = reviewed.ReviewedAt,
                        SeparationOfDutiesWaiverId = reviewed.SeparationOfDutiesWaiverId,
                    };
                    return true;
                case RiskControlTreatmentRetired retired:
                    var retirementIndex = ControlTreatments.FindIndex(item =>
                        item.TreatmentId == retired.TreatmentId && item.Status == "accepted");
                    if (retirementIndex < 0)
                        return false;
                    ControlTreatments[retirementIndex] = ControlTreatments[retirementIndex] with
                    {
                        Status = "retired",
                        RetiredBy = retired.RetiredBy,
                        RetirementRationale = retired.Rationale,
                        RetiredAt = retired.RetiredAt,
                    };
                    return true;
                case RiskReassessmentTriggered triggered:
                    var trigger = await FindingContent.ReadAsync<RiskReassessmentTriggerView>(store, tenantId,
                        triggered.TriggerContent, ct).ConfigureAwait(false);
                    if (Triggers.Any(item => item.TriggerId == trigger.TriggerId))
                        return false;
                    Triggers.Add(trigger);
                    return true;
                default:
                    return true;
            }
        }
    }

    sealed class RiskEvaluationState
    {
        readonly List<RiskAssessmentView> _assessments = [];
        readonly List<RiskAcceptanceView> _acceptances = [];
        readonly HashSet<Uuid> _assessmentIds = [];
        readonly HashSet<Uuid> _acceptanceIds = [];
        RiskTreatmentView? _treatment;
        ActorReference? _lastChangedBy;
        DateTimeOffset? _lastChangedAt;

        public IReadOnlyList<RiskAssessmentView> Assessments => _assessments;
        public IReadOnlyList<RiskAcceptanceView> Acceptances => _acceptances;
        public RiskTreatmentView? Treatment => _treatment;

        public bool Apply(DomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case RiskAssessmentRecorded assessment:
                    if (_assessmentIds.Contains(assessment.Assessment.AssessmentId))
                        return false;
                    if (assessment.Assessment.Phase != RiskEvaluation.Inherent)
                    {
                        var inherent = _assessments.LastOrDefault(static item =>
                            item.Phase == RiskEvaluation.Inherent);
                        if (inherent is null || inherent.MethodVersionId !=
                            assessment.Assessment.MethodVersionId)
                            return false;
                    }
                    if (assessment.Assessment.Phase == RiskEvaluation.Residual &&
                        _treatment is null)
                        return false;
                    _assessmentIds.Add(assessment.Assessment.AssessmentId);
                    _assessments.Add(assessment.Assessment);
                    _lastChangedBy = assessment.Assessment.Assessor;
                    _lastChangedAt = assessment.Assessment.AssessedAt;
                    return true;
                case RiskTreatmentChosen treatment:
                    if (!_assessments.Any(static item =>
                            item.Phase == RiskEvaluation.Inherent))
                        return false;
                    _treatment = treatment.Treatment;
                    _lastChangedBy = treatment.Treatment.ChosenBy;
                    _lastChangedAt = treatment.Treatment.ChosenAt;
                    return true;
                case RiskAccepted accepted:
                    var residual = _assessments.LastOrDefault(static item =>
                        item.Phase == RiskEvaluation.Residual);
                    if (_treatment is not { Kind: "accept" } || residual is null ||
                        residual.AssessmentId != accepted.Acceptance.ResidualAssessmentId ||
                        residual.Score != accepted.Acceptance.ResidualScore ||
                        !_acceptanceIds.Add(accepted.Acceptance.AcceptanceId))
                        return false;
                    _acceptances.Add(accepted.Acceptance);
                    _lastChangedBy = accepted.Acceptance.Approver;
                    _lastChangedAt = accepted.Acceptance.AcceptedAt;
                    return true;
                default:
                    return false;
            }
        }

        public RiskEvaluationView ToView(Uuid tenantId, Uuid programId, Uuid riskId,
            long revision)
        {
            return new RiskEvaluationView(tenantId, programId, riskId, revision,
                "unassessed", _assessments.AsReadOnly(), _treatment,
                _acceptances.AsReadOnly(), null, _lastChangedBy, _lastChangedAt);
        }
    }
}
