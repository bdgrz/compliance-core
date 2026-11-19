using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Verifies the exact edition start before asking the witness to decide refusal.</summary>
sealed class DecideCriteriaWitnessRefusalHandler(CriteriaLabelSourceVerifier source,
    CriteriaWitnessRefusalCommitter committer) : IRequestHandler<DecideCriteriaWitnessRefusal>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<DecideCriteriaWitnessRefusal> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.", false);
        if (request.EditionId == Uuid.Empty || request.AttemptId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(request.Identifier))
            return Failure(RequestErrorKind.Validation, "The exact witness start is required.", false);
        var owner = new CriteriaEdition(request.EditionId);
        var started = await source.ReadAsync<CriteriaPublicationStarted>(owner.Stream, request.SourcePosition,
            request.SourceEventId, ct).ConfigureAwait(false);
        if (started is null)
            return Failure(RequestErrorKind.Conflict, "The publication start is not yet observable.", true);
        if (started.Metadata.AggregateId != owner.Id || started.EditionId != request.EditionId ||
            started.PublicationAttemptId != request.AttemptId || started.WitnessIdentifier != request.Identifier)
            return Failure(RequestErrorKind.Validation, "The source start names another witness.", false);
        try
        {
            if (!await source.AllowsPublicationAsync(started, ct).ConfigureAwait(false))
                return Failure(RequestErrorKind.Conflict, "The legacy publication requires qualified recovery.", true);
        }
        catch (CriteriaSourceUnavailableException) { return Failure(RequestErrorKind.Conflict, "Publication source is unavailable.", true); }
        catch (FitzException) { return Failure(RequestErrorKind.Conflict, "Publication source is unavailable.", true); }
        catch (IOException) { return Failure(RequestErrorKind.Conflict, "Publication source is unavailable.", true); }
        catch (TimeoutException) { return Failure(RequestErrorKind.Conflict, "Publication source is unavailable.", true); }
        return await committer.RefuseAsync(started, context, ct).ConfigureAwait(false);
    }

    static Result Failure(RequestErrorKind kind, string message, bool transient) =>
        Result.Failure(new RequestError(kind, message, transient));
}
