using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Examinations.Services;

/// <summary>
///     Captures control evaluation history known by the Type I examination date. Every evaluation is its own stream,
///     folded at the existing Type I occurrence date, or an explicit authenticated close receipt cut. The whole program
///     history is read twice and a write between the reads makes the capture transient.
/// </summary>
public sealed class ControlEvaluationTypeIBaselineSourceContributor(ControlEvaluationReader evaluations)
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

        var through = context.RecordedThrough ?? new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var receiptCut = context.RecordedThrough is null ? null : context.RequireReceipts(engagement.TenantId);
        var firstRead = await evaluations.ReadStatesAsync(engagement.TenantId, engagement.ProgramId, through, receiptCut, ct)
            .ConfigureAwait(false);
        var confirmation = await evaluations.ReadStatesAsync(engagement.TenantId, engagement.ProgramId, through, receiptCut, ct)
            .ConfigureAwait(false);
        if (firstRead.Fence != confirmation.Fence)
            return SourceChanged();

        var histories = new Dictionary<Uuid, ControlEvaluationState>();
        foreach (var state in confirmation.States)
        {
            if (!controlVersions.ContainsKey(state.ControlId))
                continue;
            if (!IsValid(state, context.RecordedThrough is null ? through : DateTimeOffset.MaxValue) || !histories.TryAdd(state.EvaluationId, state))
                return SourceConflict();
        }

        if (!ValidRetestLinks(histories))
            return SourceConflict();

        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var evaluation in histories.Values.OrderBy(static evaluation => evaluation.ControlId.ToString(),
                     StringComparer.Ordinal)
                 .ThenBy(static evaluation => evaluation.StartedAt)
                 .ThenBy(static evaluation => evaluation.EvaluationId.ToString(), StringComparer.Ordinal))
        {
            try
            {
                sources.Add(new TypeIBaselineSourceReference("control_evaluation", evaluation.EvaluationId,
                    Uuid.CreateVersion5(evaluation.EvaluationId, $"control_evaluation_v1:{evaluation.Revision}"),
                    evaluation.Revision, Digest(evaluation)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException)
            {
                blockers.Add($"Control evaluation {evaluation.EvaluationId} has invalid canonical source content.");
                scopeDifferences.Add($"Control evaluation {evaluation.EvaluationId} has invalid canonical source content.");
            }
        }

        foreach (var (controlId, controlVersionId) in controlVersions)
        {
            var matching = histories.Values.Where(evaluation =>
                    evaluation.ControlId == controlId && evaluation.ControlVersionId == controlVersionId)
                .ToArray();
            var accepted = matching.Where(static evaluation => evaluation.State == ControlEvaluationStates.Accepted &&
                    evaluation.Submissions.Count > 0 &&
                    evaluation.Reviews.LastOrDefault() is { Decision: ControlEvaluationStates.Accepted })
                .OrderByDescending(static evaluation => evaluation.Reviews[^1].ReviewedAt)
                .ThenByDescending(static evaluation => evaluation.EvaluationId.ToString(), StringComparer.Ordinal)
                .FirstOrDefault();
            if (accepted is null)
            {
                blockers.Add($"Control {controlId} has no accepted evaluation for the control version effective on the Type I date.");
                continue;
            }

            var latest = accepted.Submissions[^1];
            var overall = latest.Overall;
            if (overall is ControlEvaluationStates.Ineffective or ControlEvaluationStates.NotTested)
                blockers.Add($"Control {controlId} has an accepted evaluation that is {overall} on the Type I date.");

            if (latest.Deviations.Any(static deviation => deviation.Classification == ControlEvaluationStates.Material) &&
                RetestStatus(accepted, histories.Values) != ControlEvaluationStates.Passed)
                blockers.Add($"Control {controlId} has a material evaluation deviation without a passed retest on the Type I date.");

            if (overall == ControlEvaluationStates.EffectiveWithExceptions)
                scopeDifferences.Add($"Control {controlId} has an accepted evaluation with exceptions on the Type I date.");

            if (matching.Any(evaluation => evaluation.StartedAt > accepted.StartedAt &&
                    evaluation.State != ControlEvaluationStates.Accepted))
                scopeDifferences.Add($"Control {controlId} has a newer evaluation awaiting an accepted review on the Type I date.");
        }

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(), blockers.AsReadOnly(),
                scopeDifferences.AsReadOnly()));
    }

    /// <summary>
    ///     Rechecks what the evaluation stream's own guards already enforce, so a corrupt stream fails closed: business
    ///     times fall before the examination date, a submission is the round of the step results it rests on, and a
    ///     review by the evaluator names a waiver.
    /// </summary>
    static bool IsValid(ControlEvaluationState state, DateTimeOffset through)
    {
        if (state.StartedAt == default || state.StartedAt > through || state.ControlVersionId == Uuid.Empty ||
            state.PlanVersionId == Uuid.Empty || state.PlanVersion < 1 || state.EvaluatorMemberId == Uuid.Empty ||
            state.RetestOfEvaluationId is null && state.RetestOfDeviationIds.Count != 0 ||
            ControlEvaluationRules.ValidateProcedure(state.Procedure) is not null)
            return false;
        if (state.Results.Values.Any(result => result.RecordedAt == default || result.RecordedAt > through))
            return false;
        foreach (var submission in state.Submissions)
        {
            if (submission.SubmittedAt == default || submission.SubmittedAt > through ||
                submission.Conclusions.Count != ControlEvaluationStates.Assertions.Count ||
                submission.Overall != ControlEvaluationRules.Overall(
                    submission.Conclusions.Select(static conclusion => conclusion.Conclusion)) ||
                !SupportedByStepResults(submission))
                return false;
        }

        foreach (var review in state.Reviews)
            if (review.ReviewedAt == default || review.ReviewedAt > through ||
                review.ReviewerMemberId == state.EvaluatorMemberId && review.SeparationOfDutiesWaiverId is null)
                return false;
        return true;
    }

    /// <summary>Every assertion conclusion must be one its own step results and deviations support.</summary>
    static bool SupportedByStepResults(EvaluationSubmissionView submission) =>
        submission.Steps.All(static step => step.Result is not null) && submission.Conclusions.All(conclusion =>
            ControlEvaluationRules.AllowedConclusions(
                submission.Steps.Where(step => step.Assertion == conclusion.Assertion).ToArray(),
                submission.Deviations).Contains(conclusion.Conclusion));

    static bool ValidRetestLinks(IReadOnlyDictionary<Uuid, ControlEvaluationState> histories)
    {
        foreach (var history in histories.Values)
        {
            if (history.RetestOfEvaluationId is not { } parentId)
                continue;
            if (!histories.TryGetValue(parentId, out var parent) || parent.EvaluationId == history.EvaluationId ||
                parent.ControlId != history.ControlId || parent.StartedAt >= history.StartedAt ||
                parent.State != ControlEvaluationStates.Accepted || parent.AcceptedSubmission is not { } submission ||
                history.PlanVersion < parent.PlanVersion || history.RetestPredecessor is not { } proof ||
                parent.AcceptingFact is not { } accepting || proof.EventId != accepting.EventId ||
                proof.AggregateVersion != accepting.AggregateVersion)
                return false;

            var deviationIds = submission.Deviations.Where(static deviation =>
                    deviation.Classification == ControlEvaluationStates.Material)
                .Select(static deviation => deviation.DeviationId).ToArray();
            if (deviationIds.Length == 0 || !deviationIds.SequenceEqual(history.RetestOfDeviationIds))
                return false;
        }

        return true;
    }

    static string RetestStatus(ControlEvaluationState evaluation, IEnumerable<ControlEvaluationState> evaluations) =>
        evaluation.ToView([.. evaluations.Where(candidate =>
            candidate.RetestOfEvaluationId == evaluation.EvaluationId)]).RetestStatus;

    static string Digest(ControlEvaluationState evaluation)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format_version", 2);
            writer.WriteString("tenant_id", evaluation.TenantId.ToString());
            writer.WriteString("program_id", evaluation.ProgramId.ToString());
            writer.WriteString("control_id", evaluation.ControlId.ToString());
            writer.WriteString("control_version_id", evaluation.ControlVersionId.ToString());
            writer.WriteString("evaluation_id", evaluation.EvaluationId.ToString());
            writer.WriteNumber("revision", evaluation.Revision);
            if (evaluation.RetestPredecessor is { } predecessor)
            {
                writer.WriteString("retest_predecessor_event_id", predecessor.EventId.ToString());
                writer.WriteNumber("retest_predecessor_version", predecessor.AggregateVersion);
            }

            writer.WritePropertyName("evaluation");
            WriteCanonical(writer, JsonSerializer.SerializeToElement(evaluation.ToView([]),
                ComplianceCoreJsonContext.Default.ControlEvaluationView));
            writer.WriteEndObject();
        }

        var domain = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[domain.Length + bytes.Length];
        domain.CopyTo(material, 0);
        bytes.CopyTo(material, domain.Length);
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
            "The control evaluation streams changed during the Type I baseline read.",
            isTransient: true));
}
