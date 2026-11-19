using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Fences a terminal request using its exact source facts, even when no reservation is observable.</summary>
sealed class FenceCriteriaEditionLabelRequestHandler(IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source) : IRequestHandler<FenceCriteriaEditionLabelRequest>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<FenceCriteriaEditionLabelRequest> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "Only a system reaction can fence a criteria label request.");
        var request = context.Request;
        if (request.EditionId == Uuid.Empty || request.OperationId == Uuid.Empty ||
            request.EditionRequestEventId == Uuid.Empty || request.TerminalEventId == Uuid.Empty ||
            !EditionLabelClaim.TryNormalize(request.Label, out var label) || label != request.Label)
            return Failure(RequestErrorKind.Validation, "The exact edition request and terminal fact are required.");
        var edition = new CriteriaEdition(request.EditionId);
        var intent = await source.FindAsync<DomainEvent>(edition.Stream, request.EditionRequestEventId, ct)
            .ConfigureAwait(false);
        var terminal = await source.ReadAsync<DomainEvent>(edition.Stream, request.TerminalEventPosition,
            request.TerminalEventId, ct).ConfigureAwait(false);
        if (intent is null || terminal is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact committed edition facts are not yet observable.", true));
        if (intent.Metadata.AggregateId != edition.Id || terminal.Metadata.AggregateId != edition.Id)
            return Failure(RequestErrorKind.Validation, "The source facts belong to another edition.");
        if (!CriteriaParticipantAdmission.AllowsLabel(intent))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The legacy request requires qualified recovery.", true));
        var reason = CriteriaLabelReleaseRules.AuthorizeFence(request.EditionId, request.OperationId, label,
            intent, terminal);
        if (reason is null)
            return Failure(RequestErrorKind.Validation, "The edition facts do not authorize this request fence.");

        return await executor.ExecuteAsync(new EditionLabelClaim(label, request.EditionId, request.OperationId),
            claim => AggregateOutcome.CommitOnSuccess(claim.FenceTerminalRequest(request.EditionId,
                request.OperationId, request.EditionRequestEventId, request.TerminalEventId, reason)),
            context, ct).ConfigureAwait(false);
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
