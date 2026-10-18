using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Reads the complete attributed feedback history owned by one handoff stream.</summary>
public sealed class ListAdvisorFeedbackHandler(IAggregateReader reader)
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

        var handoff = await reader.HydrateAsync(new AdvisorHandoff(request.TenantId,
            request.ProgramId, request.HandoffId), ct).ConfigureAwait(false);
        if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
            return Result<Page<AdvisorFeedbackView>>.Failure(new RequestError(
                RequestErrorKind.NotFound,
                "The advisor handoff was not found in the requested program."));

        var feedback = handoff.ReadFeedback();
        var start = 0;
        if (cursorId is { } after)
        {
            var cursorIndex = Array.FindIndex(feedback.ToArray(), item => item.FeedbackId == after);
            if (cursorIndex < 0)
                return Invalid("The advisor feedback cursor is invalid for this handoff.");
            start = cursorIndex + 1;
        }

        var limit = request.Limit ?? DefaultLimit;
        var candidates = feedback.Skip(start).Take(limit + 1).ToArray();
        var hasMore = candidates.Length > limit;
        var pageItems = candidates.Take(limit).Select(item => new AdvisorFeedbackView(
            request.TenantId, request.ProgramId, item.FeedbackId, request.HandoffId,
            item.RecordId, item.RecordVersionId, item.Kind, item.Body, item.Author,
            item.CreatedAt,
            item.Corrections?.Select(correction => new AdvisorFeedbackCorrectionView(
                correction.CorrectionId, correction.Body, correction.Reason, correction.Actor,
                correction.CorrectedAt)).ToArray() ?? [],
            item.Responses?.Select(response => new AdvisorFeedbackResponseView(response.Body,
                response.Actor, response.RespondedAt)).ToArray() ?? [],
            item.ResolvedAt, item.Disposition == AdvisorFeedbackDisposition.Current)).ToArray();
        var nextCursor = hasMore && pageItems.Length > 0
            ? pageItems[^1].AdvisorFeedbackId.ToString()
            : null;
        return Result<Page<AdvisorFeedbackView>>.Success(new Page<AdvisorFeedbackView>(pageItems,
            nextCursor));
    }

    static Result<Page<AdvisorFeedbackView>> Invalid(string message) =>
        Result<Page<AdvisorFeedbackView>>.Failure(new RequestError(RequestErrorKind.Validation,
            message));
}
