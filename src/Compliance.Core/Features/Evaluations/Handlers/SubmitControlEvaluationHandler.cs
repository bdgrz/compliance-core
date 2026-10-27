using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Evaluations.Handlers;

/// <summary>
///     Records the evaluator's personal sign-off of separate assertion conclusions; HTTP-only. Every step needs a
///     result and every minor deviation a disposition before the round can be submitted. The complete submission is
///     immutable content, and an exact retry returns the original view even after the review.
/// </summary>
public sealed class SubmitControlEvaluationHandler(NativeOperationExecutor operations,
    ControlEvaluationReader evaluations, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<SubmitControlEvaluation, ControlEvaluationView>
{
    const string CommandKind = "SubmitControlEvaluation";

    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<SubmitControlEvaluation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Control evaluation sign-off requires personal HTTP submission."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.EvaluationId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A submission requires a command id, an evaluation and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var member = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        return await EvaluationWork.RunAsync(operations, evaluations, content, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId, request.CommandId, CommandKind,
            AuthoredIntentDigest.Compute(request), request.ExpectedRevision, actor, now, async (state, token) =>
            {
                var failure = ControlEvaluationRules.CheckSubmit(state, request, actor.MemberId, member, now,
                    out var submission);
                if (failure is not null)
                    return Result<EvaluationChange>.Failure(failure.ToRequestError());
                var submitted = await NativeContent.WriteAsync(content, request.TenantId,
                    ControlEvaluationContent.SubmissionSchema, submission!, token).ConfigureAwait(false);
                var after = state.Clone();
                after.Submit(state.Revision + 1, submission!);
                return Result<EvaluationChange>.Success(new EvaluationChange(after,
                    (ControlEvaluation owner) => owner.PrepareWork(request.ExpectedRevision),
                    (ControlEvaluation owner, OperationProvenance provenance, ImmutableContentRef first,
                        ImmutableContentRef operationResult) => owner.Submit(provenance, submitted, first,
                        operationResult)));
            }, ct).ConfigureAwait(false);
    }
}
