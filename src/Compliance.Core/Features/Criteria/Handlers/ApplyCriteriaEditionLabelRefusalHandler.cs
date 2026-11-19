using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Propagates only a source-proven claim-side contention decision.</summary>
sealed class ApplyCriteriaEditionLabelRefusalHandler(IAggregateExecutor executor,
    CriteriaLabelSourceVerifier source, CriteriaLabelRefusalVerifier verifier) : IRequestHandler<ApplyCriteriaEditionLabelRefusal>
{
    internal const string Reason = "label already reserved";

    public async ValueTask<Result> HandleAsync(IRequestContext<ApplyCriteriaEditionLabelRefusal> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "A system reaction is required.", false);
        if (request.EditionId == Uuid.Empty || request.OperationId == Uuid.Empty || request.SourceEventId == Uuid.Empty ||
            !EditionLabelClaim.TryNormalize(request.Label, out var label) || label != request.Label)
            return Failure(RequestErrorKind.Validation, "The exact label refusal identity is required.", false);
        var fact = await source.ReadAsync<EditionLabelReservationRefused>(EditionLabelClaim.AddressOf(label),
            request.SourcePosition, request.SourceEventId, ct).ConfigureAwait(false);
        if (fact is null)
            return Pending();
        if (fact.EditionId != request.EditionId || fact.OperationId != request.OperationId || fact.Label != label)
            return Failure(RequestErrorKind.Validation, "The claim refusal names another edition request.", false);
        var evidence = await verifier.VerifyAsync(label, request.SourceEventId, ct).ConfigureAwait(false);
        if (evidence is null)
            return Pending();
        if (!CriteriaLabelRefusalCommitter.SameRefusal(evidence.Refusal, fact))
            return Failure(RequestErrorKind.Validation, "The refusal source facts disagree.", false);
        if (!CriteriaParticipantAdmission.AllowsLabel(evidence.Request))
            return Pending();
        var initial = evidence.Request is CriteriaEditionDraftRequested or CriteriaInitialLabelRequested;
        return await executor.ExecuteAsync(new CriteriaEdition(request.EditionId, request.OperationId), owner =>
            AggregateOutcome.CommitOnSuccess(initial
                ? owner.RecordInitialLabelRefusal(request.OperationId, request.SourceEventId, Reason)
                : owner.RefuseRename(request.OperationId, request.SourceEventId, Reason)), context, ct).ConfigureAwait(false);
    }

    static Result Pending() => Failure(RequestErrorKind.Conflict, "The exact refusal evidence is not yet observable.", true);
    static Result Failure(RequestErrorKind kind, string message, bool transient) =>
        Result.Failure(new RequestError(kind, message, transient));
}
