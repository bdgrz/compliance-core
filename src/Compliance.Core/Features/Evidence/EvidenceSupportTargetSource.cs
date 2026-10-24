using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Verifies a support link against current source-of-truth aggregate revisions.</summary>
sealed class EvidenceSupportTargetSource(IAggregateReader reader, ControlOperationsReader operations,
    ControlEvaluationReader evaluations)
{
    public ValueTask<Result> RequireExactAsync(LinkEvidenceSupport request, CancellationToken ct) =>
        request.SupportedRecordType?.Trim().ToLowerInvariant() switch
        {
            "control" => RequireControlAsync(request, ct),
            "control_version" => RequireControlVersionAsync(request, ct),
            "control_occurrence" => RequireOccurrenceAsync(request, ct),
            "control_evaluation" => RequireEvaluationAsync(request, ct),
            "period" => RequirePeriodAsync(request, ct),
            _ => ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The evidence support target type is not supported."))),
        };

    async ValueTask<Result> RequireControlAsync(LinkEvidenceSupport request, CancellationToken ct)
    {
        var control = await reader.HydrateAsync(new ControlDraft(request.TenantId,
            request.SupportedRecordId), ct).ConfigureAwait(false);
        if (!control.IsVisible || control.ProgramId != request.ProgramId)
            return Missing();
        return control.Revision == request.SupportedRecordRevision ? Result.Success : Stale();
    }

    async ValueTask<Result> RequireControlVersionAsync(LinkEvidenceSupport request,
        CancellationToken ct)
    {
        var control = await reader.HydrateAsync(new ControlDraft(request.TenantId,
            request.SupportedRecordId), ct).ConfigureAwait(false);
        if (!control.IsVisible || control.ProgramId != request.ProgramId)
            return Missing();
        var version = control.ReadVersions().FirstOrDefault(item =>
            item.Revision == request.SupportedRecordRevision);
        if (version is null)
            return Stale();
        return request.PeriodStart >= version.EffectiveFrom &&
               (version.EffectiveUntil is null || request.PeriodEnd < version.EffectiveUntil)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The evidence support period must fit within the selected control version."));
    }

    async ValueTask<Result> RequireOccurrenceAsync(LinkEvidenceSupport request,
        CancellationToken ct)
    {
        var ledger = await operations.ReadAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        if (ledger.OccurrenceControlId(request.SupportedRecordId) is not { } controlId)
            return Missing();
        var occurrence = ledger.ReadRecordedOccurrences(controlId).FirstOrDefault(item =>
            item.OccurrenceId == request.SupportedRecordId);
        if (occurrence is null || occurrence.TenantId != request.TenantId ||
            occurrence.ProgramId != request.ProgramId)
            return Missing();
        if (occurrence.Revision != request.SupportedRecordRevision)
            return Stale();
        return FitsPeriod(request, occurrence.PeriodStart, occurrence.PeriodEnd);
    }

    async ValueTask<Result> RequireEvaluationAsync(LinkEvidenceSupport request,
        CancellationToken ct)
    {
        var (_, evaluation) = await evaluations.ReadAsync(request.TenantId, request.SupportedRecordId, default, ct)
            .ConfigureAwait(false);
        if (evaluation is null || evaluation.TenantId != request.TenantId ||
            evaluation.ProgramId != request.ProgramId)
            return Missing();
        return evaluation.Revision == request.SupportedRecordRevision ? Result.Success : Stale();
    }

    async ValueTask<Result> RequirePeriodAsync(LinkEvidenceSupport request, CancellationToken ct)
    {
        var period = await reader.HydrateAsync(new TypeIIPeriod(request.TenantId,
            request.ProgramId, request.SupportedRecordId), ct).ConfigureAwait(false);
        if (period.Current is not { } current || current.TenantId != request.TenantId ||
            current.ProgramId != request.ProgramId)
            return Missing();
        if (period.Revision != request.SupportedRecordRevision)
            return Stale();
        return FitsPeriod(request, current.PeriodStart, current.PeriodEnd);
    }

    static Result FitsPeriod(LinkEvidenceSupport request, DateOnly? start, DateOnly? end) =>
        start is { } from && request.PeriodStart < from || end is { } through && request.PeriodEnd > through
            ? Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The evidence support period must fit within the selected source period."))
            : Result.Success;

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The evidence support target was not found in the requested program."));

    static Result Stale() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The requested evidence support target revision is no longer current."));
}
