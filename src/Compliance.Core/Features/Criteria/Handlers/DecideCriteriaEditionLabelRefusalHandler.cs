using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Verifies edition intent before delegating to a qualified claim-side refusal writer.</summary>
sealed class DecideCriteriaEditionLabelRefusalHandler(CriteriaLabelSourceVerifier source,
    ICriteriaLabelRefusalCommitter? committer = null) : IRequestHandler<DecideCriteriaEditionLabelRefusal>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<DecideCriteriaEditionLabelRefusal> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.", false);
        if (request.EditionId == Uuid.Empty || request.OperationId == Uuid.Empty || request.EditionEventId == Uuid.Empty ||
            !EditionLabelClaim.TryNormalize(request.Label, out var label) || label != request.Label)
            return Failure(RequestErrorKind.Validation, "The exact normalized edition request is required.", false);
        var edition = new CriteriaEdition(request.EditionId);
        var fact = await source.ReadAsync<DomainEvent>(edition.Stream, request.EditionEventPosition,
            request.EditionEventId, ct).ConfigureAwait(false);
        if (fact is null)
            return Failure(RequestErrorKind.Conflict, "The edition request is not yet observable.", true);
        if (fact.Metadata.AggregateId != edition.Id)
            return Failure(RequestErrorKind.Validation, "The request belongs to another edition.", false);
        var actor = fact switch
        {
            CriteriaEditionDraftRequested e when e.EditionId == request.EditionId && e.OperationId == request.OperationId && e.Label == label => e.Actor,
            CriteriaInitialLabelRequested e when e.EditionId == request.EditionId && e.OperationId == request.OperationId && e.Label == label => e.Actor,
            CriteriaEditionRenameRequested e when e.EditionId == request.EditionId && e.OperationId == request.OperationId && e.NewLabel == label => e.Actor,
            _ => null,
        };
        if (actor is null)
            return Failure(RequestErrorKind.Validation, "The source is not the exact edition request.", false);
        if (!CriteriaParticipantAdmission.AllowsLabel(fact))
            return Failure(RequestErrorKind.Conflict, "The legacy request requires qualified recovery.", true);
        if (committer is null)
            return Failure(RequestErrorKind.Conflict, "Qualified claim-side refusal is unavailable.", true);
        return await committer.RefuseIfContestedAsync(label, request.EditionId, request.OperationId,
            request.EditionEventId, actor, context, ct).ConfigureAwait(false);
    }

    static Result Failure(RequestErrorKind kind, string message, bool transient) =>
        Result.Failure(new RequestError(kind, message, transient));
}
