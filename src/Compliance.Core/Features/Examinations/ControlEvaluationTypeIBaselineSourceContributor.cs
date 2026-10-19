using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bdgrz.Compliance.Features.Evaluations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures control evaluation history known by the Type I examination date.</summary>
public sealed class ControlEvaluationTypeIBaselineSourceContributor(IDomainEventReader events)
    : ITypeIBaselineSourceContributor
{
    const string DigestDomain = "bdgrz.snapshot.source.control_evaluation.v1\0";

    public string Family => "control_evaluations";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null ||
            engagement.TenantId == Uuid.Empty ||
            engagement.ProgramId == Uuid.Empty || engagement.PeriodStart == default)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A control evaluation baseline read requires a complete examination scope."));

        var controlVersions = new Dictionary<Uuid, Uuid>();
        foreach (var source in context.PreviouslyCapturedSources.Where(static source =>
                     source.Kind == "control_version"))
        {
            if (source.RecordId == Uuid.Empty || source.VersionId == Uuid.Empty ||
                source.Revision < 1 || !IsDigest(source.ContentSha256) ||
                !controlVersions.TryAdd(source.RecordId, source.VersionId))
                return SourceConflict();
        }

        var stream = new EventStreamAddress(engagement.TenantId.ToString(),
            "control-evaluations", engagement.ProgramId.ToString());
        var firstRead = await ReadHistoryAsync(stream, ct).ConfigureAwait(false);
        if (!firstRead.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(firstRead.Error);
        var confirmation = await ReadHistoryAsync(stream, ct).ConfigureAwait(false);
        if (!confirmation.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(confirmation.Error);
        if (!SameHistory(firstRead.Value, confirmation.Value))
            return SourceChanged();

        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var histories = new Dictionary<Uuid, EvaluationHistory>();
        var seenEventIds = new HashSet<Uuid>();
        ulong expectedStreamRevision = 1;
        DateTimeOffset? priorOccurredOn = null;
        foreach (var domainEvent in confirmation.Value)
        {
            var metadata = domainEvent.Metadata;
            if (metadata is null || metadata.EventId == Uuid.Empty ||
                !seenEventIds.Add(metadata.EventId) || metadata.AggregateId != engagement.ProgramId ||
                metadata.AggregateVersion != expectedStreamRevision || metadata.OccurredOn == default ||
                priorOccurredOn is { } prior && metadata.OccurredOn < prior)
                return SourceConflict();
            expectedStreamRevision++;
            priorOccurredOn = metadata.OccurredOn;

            if (metadata.OccurredOn > through)
                continue;
            if (!IsKnownEvaluationEvent(domainEvent))
                return SourceConflict();
            var controlId = ControlIdFor(domainEvent);
            if (controlId is null)
                return SourceConflict();
            if (!controlVersions.ContainsKey(controlId.Value))
                continue;
            if (BusinessOccurredOn(domainEvent) is { } businessOccurredOn &&
                businessOccurredOn > through)
                return SourceConflict();
            if (!Apply(domainEvent, engagement, histories))
                return SourceConflict();
        }

        if (!ValidRetestLinks(histories))
            return SourceConflict();

        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var evaluation in histories.Values.OrderBy(static evaluation =>
                     evaluation.Started.ControlId.ToString(), StringComparer.Ordinal)
                 .ThenBy(static evaluation => evaluation.Started.StartedAt)
                 .ThenBy(static evaluation => evaluation.Started.EvaluationId.ToString(),
                     StringComparer.Ordinal))
        {
            if (!controlVersions.ContainsKey(evaluation.Started.ControlId))
                continue;

            try
            {
                sources.Add(new TypeIBaselineSourceReference("control_evaluation",
                    evaluation.Started.EvaluationId,
                    Uuid.CreateVersion5(evaluation.Started.EvaluationId,
                        $"control_evaluation_v1:{evaluation.Revision}"),
                    evaluation.Revision, Digest(evaluation)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException)
            {
                blockers.Add($"Control evaluation {evaluation.Started.EvaluationId} has invalid canonical source content.");
                scopeDifferences.Add($"Control evaluation {evaluation.Started.EvaluationId} has invalid canonical source content.");
            }
        }

        foreach (var (controlId, controlVersionId) in controlVersions)
        {
            var matching = histories.Values.Where(evaluation =>
                    evaluation.Started.ControlId == controlId &&
                    evaluation.Started.ControlVersionId == controlVersionId)
                .ToArray();
            var accepted = matching.Where(static evaluation => evaluation.State ==
                    ControlEvaluationLedger.Accepted && evaluation.LatestSubmission is not null &&
                    evaluation.LatestReview is { Decision: ControlEvaluationLedger.Accepted })
                .OrderByDescending(static evaluation => evaluation.LatestReview!.ReviewedAt)
                .ThenByDescending(static evaluation => evaluation.Started.EvaluationId.ToString(),
                    StringComparer.Ordinal)
                .FirstOrDefault();
            if (accepted is null)
            {
                blockers.Add($"Control {controlId} has no accepted evaluation for the control version effective on the Type I date.");
                continue;
            }

            var overall = accepted.LatestSubmission!.Overall;
            if (overall is ControlEvaluationLedger.Ineffective or ControlEvaluationLedger.NotTested)
                blockers.Add($"Control {controlId} has an accepted evaluation that is {overall} on the Type I date.");

            if (accepted.LatestSubmission.Deviations.Any(static deviation =>
                    deviation.Classification == ControlEvaluationLedger.Material) &&
                RetestStatus(accepted, histories.Values) != ControlEvaluationLedger.Passed)
                blockers.Add($"Control {controlId} has a material evaluation deviation without a passed retest on the Type I date.");

            if (overall == ControlEvaluationLedger.EffectiveWithExceptions)
                scopeDifferences.Add($"Control {controlId} has an accepted evaluation with exceptions on the Type I date.");

            if (matching.Any(evaluation => evaluation.Started.StartedAt > accepted.Started.StartedAt &&
                    evaluation.State != ControlEvaluationLedger.Accepted))
                scopeDifferences.Add($"Control {controlId} has a newer evaluation awaiting an accepted review on the Type I date.");
        }

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(), blockers.AsReadOnly(),
                scopeDifferences.AsReadOnly()));
    }

    async ValueTask<Result<IReadOnlyList<DomainEvent>>> ReadHistoryAsync(
        EventStreamAddress stream, CancellationToken ct)
    {
        var history = new List<DomainEvent>();
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream || record.Event is null)
                return Result<IReadOnlyList<DomainEvent>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The control evaluation stream returned an invalid record."));
            history.Add(record.Event);
        }
        return Result<IReadOnlyList<DomainEvent>>.Success(history.AsReadOnly());
    }

    static bool SameHistory(IReadOnlyList<DomainEvent> left, IReadOnlyList<DomainEvent> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            var first = left[index];
            var second = right[index];
            if (first.GetType() != second.GetType() || first.Metadata != second.Metadata ||
                !first.Equals(second))
                return false;
        }
        return true;
    }

    static bool IsKnownEvaluationEvent(DomainEvent domainEvent) => domainEvent is
        ControlEvaluationStarted or ControlEvaluationStepRecorded or
        ControlEvaluationDeviationDisposed or ControlEvaluationSubmitted or
        ControlEvaluationReviewed;

    static Uuid? ControlIdFor(DomainEvent domainEvent) => domainEvent switch
    {
        ControlEvaluationStarted started => started.ControlId,
        ControlEvaluationStepRecorded step => step.ControlId,
        ControlEvaluationDeviationDisposed disposed => disposed.ControlId,
        ControlEvaluationSubmitted submitted => submitted.ControlId,
        ControlEvaluationReviewed reviewed => reviewed.ControlId,
        _ => null,
    };

    static DateTimeOffset? BusinessOccurredOn(DomainEvent domainEvent) => domainEvent switch
    {
        ControlEvaluationStarted started => started.StartedAt,
        ControlEvaluationStepRecorded step => step.Result?.RecordedAt,
        ControlEvaluationSubmitted submitted => submitted.Submission?.SubmittedAt,
        ControlEvaluationReviewed reviewed => reviewed.Review?.ReviewedAt,
        _ => null,
    };

    static bool ValidRetestLinks(IReadOnlyDictionary<Uuid, EvaluationHistory> histories)
    {
        foreach (var history in histories.Values)
        {
            if (history.Started.RetestOfEvaluationId is not { } parentId)
                continue;
            if (!histories.TryGetValue(parentId, out var parent) ||
                parent.Started.EvaluationId == history.Started.EvaluationId ||
                parent.Started.ControlId != history.Started.ControlId ||
                parent.Started.StartedAt >= history.Started.StartedAt ||
                parent.State != ControlEvaluationLedger.Accepted ||
                parent.LatestSubmission is not { } submission ||
                submission.Deviations is null ||
                parent.Started.PlanVersion is not { } originalPlanVersion ||
                history.Started.PlanVersion is not { } retestPlanVersion ||
                retestPlanVersion < originalPlanVersion ||
                history.Started.RetestOfDeviationIds is null)
                return false;

            var deviationIds = submission.Deviations.Where(static deviation =>
                    deviation.Classification == ControlEvaluationLedger.Material)
                .Select(static deviation => deviation.DeviationId).ToArray();
            if (deviationIds.Length == 0 ||
                !deviationIds.SequenceEqual(history.Started.RetestOfDeviationIds))
                return false;
        }
        return true;
    }

    static bool Apply(DomainEvent domainEvent, ExaminationEngagementView engagement,
        IDictionary<Uuid, EvaluationHistory> histories)
    {
        switch (domainEvent)
        {
            case ControlEvaluationStarted started:
                if (started.TenantId != engagement.TenantId ||
                    started.ProgramId != engagement.ProgramId || started.EvaluationId == Uuid.Empty ||
                    started.ControlId == Uuid.Empty || started.Revision != 1 ||
                    started.ControlVersionId == Uuid.Empty || started.PlanVersionId is not { } planId ||
                    planId == Uuid.Empty || started.PlanVersion is not >= 1 ||
                    started.EvaluatorMemberId == Uuid.Empty || started.StartedBy is null ||
                    started.RetestOfDeviationIds is null ||
                    started.RetestOfEvaluationId is null && started.RetestOfDeviationIds.Count != 0 ||
                    started.StartedAt == default || ControlEvaluationLedger.ValidateProcedure(
                        started.Steps) is not null)
                    return false;
                return histories.TryAdd(started.EvaluationId, new EvaluationHistory(started));

            case ControlEvaluationStepRecorded step:
                if (!TryNext(step.TenantId, step.ProgramId, step.ControlId, step.EvaluationId,
                        step.Revision, engagement, histories, out var stepHistory) ||
                    stepHistory.State != ControlEvaluationLedger.InProgress ||
                    step.Result is null || step.Result.Round != stepHistory.Round ||
                    step.Result.RecordedAt == default ||
                    !EnumishResult(step.Result.Result) || !IsBoundedText(step.Result.Rationale) ||
                    !AreItems(step.Result.InspectedItems,
                        required: step.Result.Result != ControlEvaluationLedger.NotTested) ||
                    StepIndex(stepHistory.Started, step.StepId) is null ||
                    (step.Result.Result == ControlEvaluationLedger.NotMet
                        ? step.Deviation is null || !ValidNewDeviation(stepHistory, step)
                        : step.Deviation is not null))
                    return false;
                stepHistory.Revision = step.Revision;
                stepHistory.Results[step.StepId] = step.Result;
                stepHistory.Deviations.RemoveAll(deviation => deviation.StepId == step.StepId &&
                    deviation.Round == stepHistory.Round);
                if (step.Deviation is not null)
                    stepHistory.Deviations.Add(step.Deviation);
                stepHistory.Events.Add(step);
                return true;

            case ControlEvaluationDeviationDisposed disposed:
                if (!TryNext(disposed.TenantId, disposed.ProgramId, disposed.ControlId,
                        disposed.EvaluationId, disposed.Revision, engagement, histories,
                        out var disposedHistory) ||
                    disposedHistory.State != ControlEvaluationLedger.InProgress ||
                    disposed.DeviationId == Uuid.Empty ||
                    disposed.Disposition is not (ControlEvaluationLedger.Corrected or
                        ControlEvaluationLedger.AcceptedWithWaiver) ||
                    !IsBoundedText(disposed.Rationale) ||
                    (CurrentDeviations(disposedHistory) ?? []).FirstOrDefault(deviation =>
                        deviation.DeviationId == disposed.DeviationId) is not
                        {
                            Classification: ControlEvaluationLedger.Minor, Status: not
                            ControlEvaluationLedger.Dispositioned
                        } current ||
                    disposed.Disposition == ControlEvaluationLedger.Corrected &&
                        disposed.WaiverId is not null ||
                    disposed.Disposition == ControlEvaluationLedger.AcceptedWithWaiver &&
                        (disposed.WaiverId is null || disposed.WaiverId == Uuid.Empty))
                    return false;
                disposedHistory.Revision = disposed.Revision;
                var index = disposedHistory.Deviations.FindIndex(deviation =>
                    deviation.DeviationId == current.DeviationId);
                disposedHistory.Deviations[index] = current with
                {
                    Status = ControlEvaluationLedger.Dispositioned,
                    Disposition = disposed.Disposition,
                    DispositionRationale = disposed.Rationale,
                    WaiverId = disposed.WaiverId,
                };
                disposedHistory.Events.Add(disposed);
                return true;

            case ControlEvaluationSubmitted submitted:
                if (!TryNext(submitted.TenantId, submitted.ProgramId, submitted.ControlId,
                        submitted.EvaluationId, submitted.Revision, engagement, histories,
                        out var submittedHistory) ||
                    submittedHistory.State != ControlEvaluationLedger.InProgress ||
                    submitted.Submission is null || submitted.Submission.Round != submittedHistory.Round ||
                    submitted.Submission.SubmittedAt == default ||
                    !SubmissionMatches(submittedHistory, submitted.Submission) ||
                    submitted.Submission.Overall is not (ControlEvaluationLedger.Effective or
                        ControlEvaluationLedger.EffectiveWithExceptions or
                        ControlEvaluationLedger.Ineffective or ControlEvaluationLedger.NotTested))
                    return false;
                submittedHistory.Revision = submitted.Revision;
                submittedHistory.State = ControlEvaluationLedger.Submitted;
                submittedHistory.LatestSubmission = submitted.Submission;
                submittedHistory.Events.Add(submitted);
                return true;

            case ControlEvaluationReviewed reviewed:
                if (!TryNext(reviewed.TenantId, reviewed.ProgramId, reviewed.ControlId,
                        reviewed.EvaluationId, reviewed.Revision, engagement, histories,
                        out var reviewedHistory) ||
                    reviewedHistory.State != ControlEvaluationLedger.Submitted ||
                    reviewed.Review is null || reviewed.Review.Round != reviewedHistory.Round ||
                    reviewed.Review.ReviewedAt == default ||
                    reviewed.Review.DecisionId == Uuid.Empty ||
                    reviewed.Review.ReviewerMemberId == Uuid.Empty ||
                    !IsBoundedText(reviewed.Review.Rationale) || reviewed.Review.ReviewedBy is null ||
                    reviewed.Review.ReviewerMemberId == reviewedHistory.Started.EvaluatorMemberId &&
                        reviewed.Review.SeparationOfDutiesWaiverId is null ||
                    reviewed.Review.Decision is not
                        (ControlEvaluationLedger.Accepted or ControlEvaluationLedger.Rejected or
                         ControlEvaluationLedger.ChangesRequested))
                    return false;
                reviewedHistory.Revision = reviewed.Revision;
                reviewedHistory.LatestReview = reviewed.Review;
                if (reviewed.Review.Decision == ControlEvaluationLedger.Accepted)
                    reviewedHistory.State = ControlEvaluationLedger.Accepted;
                else
                {
                    reviewedHistory.State = ControlEvaluationLedger.InProgress;
                    reviewedHistory.Round++;
                    if (reviewed.Review.Decision == ControlEvaluationLedger.Rejected)
                        reviewedHistory.Results.Clear();
                }
                reviewedHistory.Events.Add(reviewed);
                return true;

            default:
                return false;
        }
    }

    static bool TryNext(Uuid tenantId, Uuid programId, Uuid controlId, Uuid evaluationId,
        long revision, ExaminationEngagementView engagement,
        IDictionary<Uuid, EvaluationHistory> histories, out EvaluationHistory history)
    {
        if (tenantId == engagement.TenantId && programId == engagement.ProgramId &&
            histories.TryGetValue(evaluationId, out history!) &&
            history.Started.ControlId == controlId && revision == history.Revision + 1)
            return true;
        history = null!;
        return false;
    }

    static bool EnumishResult(string result) => result is ControlEvaluationLedger.Met or
        ControlEvaluationLedger.NotMet or ControlEvaluationLedger.NotTested;

    static int? StepIndex(ControlEvaluationStarted started, Uuid stepId)
    {
        for (var index = 0; index < started.Steps.Count; index++)
            if (ControlEvaluationLedger.StepId(started.EvaluationId, index) == stepId)
                return index;
        return null;
    }

    static bool ValidNewDeviation(EvaluationHistory history, ControlEvaluationStepRecorded step)
    {
        var deviation = step.Deviation;
        if (deviation is null || deviation.DeviationId != Uuid.CreateVersion5(
                step.EvaluationId, "deviation-" + step.StepId) || deviation.StepId != step.StepId ||
            deviation.Round != history.Round || deviation.Classification is not
                (ControlEvaluationLedger.Minor or ControlEvaluationLedger.Material) ||
            !IsBoundedText(deviation.Description) || deviation.Description != deviation.Description.Trim() ||
            deviation.DetectedAt != step.Result.RecordedAt ||
            deviation.DetectedBy != step.Result.RecordedBy || deviation.Disposition is not null ||
            deviation.DispositionRationale is not null || deviation.WaiverId is not null)
            return false;
        return deviation.Classification == ControlEvaluationLedger.Material
            ? deviation.Status == ControlEvaluationLedger.PendingRouting &&
              deviation.FindingId == Uuid.CreateVersion5(deviation.DeviationId, "finding")
            : deviation.Status == ControlEvaluationLedger.PendingDisposition &&
              deviation.FindingId is null;
    }

    static bool SubmissionMatches(EvaluationHistory history, EvaluationSubmissionView submission)
    {
        if (submission.Steps is null || submission.Deviations is null ||
            submission.Conclusions is null || submission.SubmittedBy is null ||
            submission.Steps.Count != history.Started.Steps.Count ||
            !IsBoundedText(submission.Overall) ||
            submission.Steps.Any(static step => step?.Result is not { } result ||
                result.RecordedAt == default))
            return false;

        var steps = new ControlEvaluationStepView[history.Started.Steps.Count];
        for (var index = 0; index < history.Started.Steps.Count; index++)
        {
            var procedure = history.Started.Steps[index];
            var stepId = ControlEvaluationLedger.StepId(history.Started.EvaluationId, index);
            var result = history.Results.GetValueOrDefault(stepId);
            steps[index] = new ControlEvaluationStepView(stepId, index, procedure.Assertion,
                procedure.Method, procedure.InspectedItems, procedure.ExpectedCondition, result);
            if (result is null || result.RecordedAt > submission.SubmittedAt ||
                !MatchesStep(steps[index], submission.Steps[index]))
                return false;
        }

        var deviations = CurrentDeviations(history);
        if (deviations is null || !submission.Deviations.SequenceEqual(deviations) ||
            submission.Conclusions.Count != ControlEvaluationLedger.Assertions.Count ||
            ControlEvaluationLedger.Assertions.Any(assertion =>
                submission.Conclusions.Count(conclusion => conclusion is not null &&
                    conclusion.Assertion == assertion) != 1))
            return false;

        foreach (var conclusion in submission.Conclusions)
        {
            if (conclusion is null || !IsBoundedText(conclusion.Rationale))
                return false;
            var supported = ControlEvaluationLedger.AllowedConclusions(
                steps.Where(step => step.Assertion == conclusion.Assertion).ToArray(), deviations);
            if (!supported.Contains(conclusion.Conclusion))
                return false;
        }

        return submission.Overall == ControlEvaluationLedger.Overall(
            submission.Conclusions.Select(static conclusion => conclusion.Conclusion));
    }

    static bool MatchesStep(ControlEvaluationStepView expected, ControlEvaluationStepView actual) =>
        actual is not null && actual.StepId == expected.StepId && actual.Index == expected.Index &&
        actual.Assertion == expected.Assertion && actual.Method == expected.Method &&
        actual.ExpectedCondition == expected.ExpectedCondition && actual.InspectedItems is not null &&
        actual.InspectedItems.SequenceEqual(expected.InspectedItems) &&
        actual.Result is { } actualResult && expected.Result is { } expectedResult &&
        actualResult.Round == expectedResult.Round && actualResult.Result == expectedResult.Result &&
        actualResult.Rationale == expectedResult.Rationale &&
        actualResult.RecordedBy == expectedResult.RecordedBy &&
        actualResult.RecordedAt == expectedResult.RecordedAt &&
        actualResult.InspectedItems is not null &&
        actualResult.InspectedItems.SequenceEqual(expectedResult.InspectedItems);

    static EvaluationDeviationView[]? CurrentDeviations(EvaluationHistory history)
    {
        var current = new List<EvaluationDeviationView>();
        foreach (var (stepId, result) in history.Results)
        {
            if (result.Result != ControlEvaluationLedger.NotMet)
                continue;
            var deviation = history.Deviations.LastOrDefault(candidate =>
                candidate.StepId == stepId);
            if (deviation is null)
                return null;
            current.Add(deviation);
        }
        return current.OrderBy(static deviation => deviation.DetectedAt).ToArray();
    }

    static bool AreItems(IReadOnlyList<EvaluationInspectedItem>? items, bool required) =>
        items is not null && (!required || items.Count > 0) && items.Count <= 50 &&
        items.All(static item => item is not null && (item.Kind is "record" or "artifact" or
            "boundary" or "commitment" or "risk" or "criterion" or "control" or "policy" or
            "provider" or "evidence") && IsBoundedText(item.Reference) && item.Reference.Trim().Length <= 2000 &&
            IsBoundedText(item.Version) && item.Version.Trim().Length <= 200);

    static bool IsBoundedText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 4000;

    static string RetestStatus(EvaluationHistory evaluation,
        IEnumerable<EvaluationHistory> evaluations)
    {
        if (evaluation.LatestSubmission is not { } accepted ||
            !accepted.Deviations.Any(static deviation =>
                deviation.Classification == ControlEvaluationLedger.Material))
            return ControlEvaluationLedger.NotRequired;
        var retests = evaluations.Where(candidate =>
                candidate.Started.RetestOfEvaluationId == evaluation.Started.EvaluationId)
            .OrderBy(static candidate => candidate.Started.StartedAt)
            .ThenBy(static candidate => candidate.Started.EvaluationId.ToString(),
                StringComparer.Ordinal).ToArray();
        if (retests.Length == 0)
            return ControlEvaluationLedger.Required;
        var latest = retests[^1];
        if (latest.State != ControlEvaluationLedger.Accepted || latest.LatestSubmission is null)
            return ControlEvaluationLedger.InProgress;
        return latest.LatestSubmission.Overall is ControlEvaluationLedger.Effective or
            ControlEvaluationLedger.EffectiveWithExceptions
            ? ControlEvaluationLedger.Passed
            : ControlEvaluationLedger.Failed;
    }

    static string Digest(EvaluationHistory evaluation)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format_version", 1);
            writer.WriteString("tenant_id", evaluation.Started.TenantId.ToString());
            writer.WriteString("program_id", evaluation.Started.ProgramId.ToString());
            writer.WriteString("control_id", evaluation.Started.ControlId.ToString());
            writer.WriteString("control_version_id", evaluation.Started.ControlVersionId.ToString());
            writer.WriteString("evaluation_id", evaluation.Started.EvaluationId.ToString());
            writer.WriteNumber("revision", evaluation.Revision);
            writer.WritePropertyName("events");
            writer.WriteStartArray();
            foreach (var domainEvent in evaluation.Events)
            {
                writer.WriteStartObject();
                writer.WriteNumber("stream_revision", domainEvent.Metadata.AggregateVersion);
                writer.WriteString("occurred_at", domainEvent.Metadata.OccurredOn);
                writer.WriteString("type", domainEvent.GetType().FullName);
                writer.WritePropertyName("payload");
                WriteCanonical(writer, Serialize(domainEvent));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var domain = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[domain.Length + bytes.Length];
        domain.CopyTo(material, 0);
        bytes.CopyTo(material, domain.Length);
        return Convert.ToHexStringLower(SHA256.HashData(material));
    }

    static JsonElement Serialize(DomainEvent domainEvent) => domainEvent switch
    {
        ControlEvaluationStarted started => JsonSerializer.SerializeToElement(started,
            ComplianceCoreJsonContext.Default.ControlEvaluationStarted),
        ControlEvaluationStepRecorded step => JsonSerializer.SerializeToElement(step,
            ComplianceCoreJsonContext.Default.ControlEvaluationStepRecorded),
        ControlEvaluationDeviationDisposed disposed => JsonSerializer.SerializeToElement(disposed,
            ComplianceCoreJsonContext.Default.ControlEvaluationDeviationDisposed),
        ControlEvaluationSubmitted submitted => JsonSerializer.SerializeToElement(submitted,
            ComplianceCoreJsonContext.Default.ControlEvaluationSubmitted),
        ControlEvaluationReviewed reviewed => JsonSerializer.SerializeToElement(reviewed,
            ComplianceCoreJsonContext.Default.ControlEvaluationReviewed),
        _ => throw new NotSupportedException("Unsupported control evaluation event."),
    };

    static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .Where(static property => !property.Name.Equals("metadata",
                                 StringComparison.OrdinalIgnoreCase))
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
                if (element.TryGetInt64(out var number))
                    writer.WriteNumberValue(number);
                else if (element.TryGetDecimal(out var decimalNumber))
                    writer.WriteNumberValue(decimalNumber);
                else
                    throw new InvalidOperationException(
                        "Control evaluation snapshot numbers exceed supported precision.");
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
                    "Unsupported content entered a control evaluation snapshot.");
        }
    }

    static bool IsDigest(string value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The control evaluation history changed or did not match the program source."));

    static Result<TypeIBaselineSourceContribution> SourceChanged() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The control evaluation stream changed during the Type I baseline read.",
            isTransient: true));

    sealed class EvaluationHistory(ControlEvaluationStarted started)
    {
        public ControlEvaluationStarted Started { get; } = started;
        public List<DomainEvent> Events { get; } = [started];
        public long Revision { get; set; } = started.Revision;
        public string State { get; set; } = ControlEvaluationLedger.InProgress;
        public int Round { get; set; } = 1;
        public Dictionary<Uuid, EvaluationStepResultView> Results { get; } = [];
        public List<EvaluationDeviationView> Deviations { get; } = [];
        public EvaluationSubmissionView? LatestSubmission { get; set; }
        public ControlEvaluationReviewView? LatestReview { get; set; }
    }
}
