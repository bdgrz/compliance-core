using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>
///     What an evaluation command decided over the folded state: the state after its fact, how to guard the owner and
///     how to raise the fact once the owner accepts it.
/// </summary>
sealed record EvaluationChange(ControlEvaluationState After, Func<ControlEvaluation, Result> Prepare,
    Func<ControlEvaluation, OperationProvenance, ImmutableContentRef, ImmutableContentRef, Result> Raise);

/// <summary>
///     Runs the root native admission of every command on an existing evaluation: resolve the retry from the evaluation's
///     own accepted facts, decide the product rules over its folded state, freeze the complete resulting view as the
///     first response and append one native fact at the exact logical revision.
/// </summary>
static class EvaluationWork
{
    public static async ValueTask<Result<ControlEvaluationView>> RunAsync(NativeOperationExecutor operations,
        ControlEvaluationReader evaluations, IImmutableContentStore content, IExecutionContext context, Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid evaluationId, Uuid commandId, string commandKind, string intentDigest,
        long expectedRevision, OperationsActor actor, DateTimeOffset now,
        Func<ControlEvaluationState, CancellationToken, ValueTask<Result<EvaluationChange>>> decide,
        CancellationToken ct)
    {
        var (current, state) = await evaluations.ReadAsync(tenantId, evaluationId, commandId, ct).ConfigureAwait(false);
        if (state is null || !current.Belongs(programId, controlId))
            return Result<ControlEvaluationView>.Failure(EvaluationNotFound());
        var provenance = RootOperation.Provenance(commandId, commandKind, current.Stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), expectedRevision, intentDigest, now);
        var binding = AcceptedOperationBinding.Of(provenance);

        EvaluationChange? change = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var decided = await decide(state, ct).ConfigureAwait(false);
            if (!decided.IsSuccess)
                return Result<ControlEvaluationView>.Failure(decided.Error!);
            change = decided.Value!;
            (response, result) = await NativeContent.FreezeAsync(content, tenantId, ControlEvaluationContent.ViewSchema,
                change.After.ToView([]), ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlEvaluation>(binding,
            () => new ControlEvaluation(tenantId, evaluationId, commandId), owner => owner.Acceptance,
            owner => Prepare(owner, change, response),
            (ControlEvaluation owner, ImmutableContentRef frozen) => change!.Raise(owner, provenance, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<ControlEvaluationView>(content, tenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    public static RequestError EvaluationNotFound() =>
        new(RequestErrorKind.NotFound, "The control evaluation was not found.");

    static Result<FirstResponseDescriptor> Prepare(ControlEvaluation owner, EvaluationChange? change,
        FirstResponseDescriptor? response)
    {
        if (change is null || response is null)
            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evaluation changed while the command was prepared; reload and retry.", true));
        var prepared = change.Prepare(owner);
        return prepared.IsSuccess
            ? Result<FirstResponseDescriptor>.Success(response)
            : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
    }
}
