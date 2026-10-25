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
        var stream = new ControlEvaluation(tenantId, evaluationId).Stream;
        var provenance = RootOperation.Provenance(commandId, commandKind, stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), expectedRevision, intentDigest, now);
        var binding = AcceptedOperationBinding.Of(provenance);

        EvaluationChange? change = null;
        EvaluationPreparedCut? preparedCut = null;
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlEvaluation>(binding,
            () => new ControlEvaluation(tenantId, evaluationId, commandId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                if (!owner.Belongs(programId, controlId))
                    return Result<FirstResponseDescriptor>.Failure(EvaluationNotFound());
                var state = await evaluations.ReadStateAsync(owner, token).ConfigureAwait(false);
                if (state is null)
                    return Result<FirstResponseDescriptor>.Failure(EvaluationNotFound());
                var decided = await decide(state, token).ConfigureAwait(false);
                if (!decided.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(decided.Error!);
                change = decided.Value!;
                var prepared = change.Prepare(owner);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var (response, frozenResult) = await NativeContent.FreezeAsync(content, tenantId,
                    ControlEvaluationContent.ViewSchema, change.After.ToView([]), token).ConfigureAwait(false);
                result = frozenResult;
                preparedCut = EvaluationPreparedCut.Capture(owner);
                return Result<FirstResponseDescriptor>.Success(response);
            },
            (ControlEvaluation owner, ImmutableContentRef frozen) =>
            {
                if (preparedCut?.Matches(owner) != true)
                    return EvaluationPreparedCut.Changed();
                return change!.Raise(owner, provenance, frozen, result!);
            }), context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<ControlEvaluationView>(content, tenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    public static RequestError EvaluationNotFound() =>
        new(RequestErrorKind.NotFound, "The control evaluation was not found.");

}
