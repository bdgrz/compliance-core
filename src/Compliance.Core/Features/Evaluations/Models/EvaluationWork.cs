using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Models;

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
