using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Reads the complete attributed feedback history of one handoff. The handoff lists the feedback it admitted in
///     admission order and each admitted contribution is read from its own thread; a contribution whose thread is not
///     materialized yet is listed once its independent materialization completes. A contribution is current only while
///     its exact source version is still selected and current.
/// </summary>
public sealed class ListAdvisorFeedbackHandler(IAggregateReader reader, AdvisorHandoffReader handoffs,
    IAdvisorRecordVersionResolver versions)
    : IRequestHandler<ListAdvisorFeedback, Page<AdvisorFeedbackView>>
{
    const int DefaultLimit = 50;
    const int MaximumLimit = 200;

    public async ValueTask<Result<Page<AdvisorFeedbackView>>> HandleAsync(
        IRequestContext<ListAdvisorFeedback> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.HandoffId == Uuid.Empty)
            return Invalid("An advisor feedback read requires tenant, program, and handoff identities.");
        if (request.Limit is < 1 or > MaximumLimit)
            return Invalid("The advisor feedback list limit must be between 1 and 200.");

        Uuid? cursorId = null;
        if (request.Cursor is not null)
        {
            if (!Uuid.TryParse(request.Cursor, CultureInfo.InvariantCulture, out var parsed) ||
                parsed == Uuid.Empty)
                return Invalid("The advisor feedback cursor is invalid.");
            cursorId = parsed;
        }

        var handoff = await handoffs.ReadAsync(request.TenantId, request.ProgramId, request.HandoffId, ct)
            .ConfigureAwait(false);
        if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
            return Result<Page<AdvisorFeedbackView>>.Failure(new RequestError(
                RequestErrorKind.NotFound,
                "The advisor handoff was not found in the requested program."));

        var admitted = handoff.Handoff.Admitted.Select(static fact => fact.Operation.LogicalId).ToArray();
        var start = 0;
        if (cursorId is { } after)
        {
            var cursorIndex = Array.IndexOf(admitted, after);
            if (cursorIndex < 0)
                return Invalid("The advisor feedback cursor is invalid for this handoff.");
            start = cursorIndex + 1;
        }

        var limit = request.Limit ?? DefaultLimit;
        var threads = new List<AdvisorFeedbackThread>(limit + 1);
        for (var index = start; index < admitted.Length && threads.Count <= limit; index++)
        {
            var thread = await reader.HydrateAsync(new AdvisorFeedbackThread(request.TenantId, request.ProgramId,
                request.HandoffId, admitted[index]), ct).ConfigureAwait(false);
            if (thread.IsMaterialized)
                threads.Add(thread);
        }

        var hasMore = threads.Count > limit;
        var pageThreads = threads.Take(limit).ToArray();
        var currentSources = new HashSet<AdvisorRecordVersion>();
        foreach (var source in pageThreads.Select(thread => SourceOf(thread.Recorded!))
                     .Where(source => handoff.Selection.Contains(source) && source.IsSupported).Distinct())
        {
            var current = await versions.RequireCurrentAsync(request.TenantId,
                request.ProgramId, [source], ct).ConfigureAwait(false);
            if (current.IsSuccess)
                currentSources.Add(source);
            else if (current.Error.IsTransient)
                return Result<Page<AdvisorFeedbackView>>.Failure(current.Error);
        }

        var pageItems = pageThreads.Select(thread =>
        {
            var recorded = thread.Recorded!;
            var source = SourceOf(recorded);
            return new AdvisorFeedbackView(request.TenantId, request.ProgramId, thread.FeedbackId,
                request.HandoffId, recorded.RecordId, recorded.RecordVersionId, recorded.Kind, recorded.Body,
                recorded.Author, recorded.CreatedAt,
                thread.Corrections.Select(correction => new AdvisorFeedbackCorrectionView(correction.CorrectionId,
                    correction.CorrectedBody, correction.CorrectionReason, correction.CorrectedBy,
                    correction.CorrectedAt)).ToArray(),
                thread.Responses.Select(response => new AdvisorFeedbackResponseView(response.Response,
                    response.RespondedBy, response.RespondedAt)).ToArray(),
                thread.Resolved?.ResolvedAt, handoff.Selection.Contains(source) &&
                    currentSources.Contains(source),
                recorded.SelectedSource, thread.Resolved?.ResolvedBy);
        }).ToArray();
        var nextCursor = hasMore && pageItems.Length > 0
            ? pageItems[^1].AdvisorFeedbackId.ToString()
            : null;
        return Result<Page<AdvisorFeedbackView>>.Success(new Page<AdvisorFeedbackView>(pageItems, nextCursor));
    }

    static AdvisorRecordVersion SourceOf(AdvisorFeedbackRecorded recorded) =>
        recorded.SelectedSource ?? new AdvisorRecordVersion(recorded.RecordId, recorded.RecordVersionId);

    static Result<Page<AdvisorFeedbackView>> Invalid(string message) =>
        Result<Page<AdvisorFeedbackView>>.Failure(new RequestError(RequestErrorKind.Validation,
            message));
}
