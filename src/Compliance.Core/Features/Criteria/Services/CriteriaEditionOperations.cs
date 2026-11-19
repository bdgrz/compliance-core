using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Admits and executes edition commands with server-produced, epoch-bound authority.</summary>
public sealed class CriteriaEditionOperations(IAggregateReader aggregates, IAggregateExecutor executor,
    ICriteriaOperationReader receipts, IAccessAdmissionAuthority? authority = null,
    ICriteriaAuthoringReadiness? readiness = null)
{
    public ValueTask<Result<CriteriaOperationReceipt>> DraftAsync(IRequestContext<DraftCriteriaEdition> context, CancellationToken ct)
    {
        if (context.Request.Framework != "tsc" || !EditionLabelClaim.TryNormalize(context.Request.Label, out var label))
            return Invalid();
        var request = context.Request with { Label = label };
        return ExecuteAsync(context, request, request.EditionId, request.OperationId,
            (owner, actor, evidence, generation) => owner.Draft(request.OperationId, request.Framework, label, actor, evidence, generation), ct);
    }

    public ValueTask<Result<CriteriaOperationReceipt>> ChooseLabelAsync(IRequestContext<ChooseInitialEditionLabel> context, CancellationToken ct)
    {
        if (!EditionLabelClaim.TryNormalize(context.Request.Label, out var label))
            return Invalid();
        var request = context.Request with { Label = label };
        return ExecuteAsync(context, request, request.EditionId, request.OperationId,
            (owner, actor, evidence, generation) => owner.ChooseInitialLabel(request.OperationId, label, actor, evidence, generation), ct);
    }

    public ValueTask<Result<CriteriaOperationReceipt>> CancelDraftAsync(IRequestContext<CancelCriteriaEditionDraftRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        return ExecuteAsync(context, request, request.EditionId, request.OperationId,
            (owner, actor, evidence, generation) => owner.CancelDraftRequest(request.OperationId, request.TargetOperationId, actor, evidence, generation), ct);
    }

    public ValueTask<Result<CriteriaOperationReceipt>> RenameAsync(IRequestContext<BeginCriteriaEditionRename> context, CancellationToken ct)
    {
        if (!EditionLabelClaim.TryNormalize(context.Request.Label, out var label))
            return Invalid();
        var request = context.Request with { Label = label };
        return ExecuteAsync(context, request, request.EditionId, request.OperationId,
            (owner, actor, evidence, generation) => owner.BeginRename(request.OperationId, label, actor, evidence, generation), ct);
    }

    public ValueTask<Result<CriteriaOperationReceipt>> CancelRenameAsync(IRequestContext<CancelCriteriaEditionRename> context, CancellationToken ct)
    {
        var request = context.Request;
        return ExecuteAsync(context, request, request.EditionId, request.OperationId,
            (owner, actor, evidence, generation) => owner.CancelRename(request.OperationId, request.RenameOperationId, actor, evidence, generation), ct);
    }

    public ValueTask<Result<CriteriaOperationReceipt>> AbandonAsync(IRequestContext<AbandonCriteriaEditionDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return ExecuteAsync(context, request, request.EditionId, request.OperationId,
            (owner, actor, evidence, generation) => owner.Abandon(request.OperationId, actor, evidence, generation), ct);
    }

    public ValueTask<Result<CriteriaOperationReceipt>> PublishAsync(IRequestContext<BeginCriteriaPublication> context, CancellationToken ct)
    {
        var request = context.Request;
        return ExecuteAsync(context, request, request.EditionId, request.AttemptId,
            (owner, actor, evidence, generation) => owner.BeginPublication(request.AttemptId, request.WitnessIdentifier,
                request.WitnessIncarnation, actor, evidence, generation), ct);
    }

    async ValueTask<Result<CriteriaOperationReceipt>> ExecuteAsync<TRequest>(IRequestContext<TRequest> context,
        TRequest canonical, Uuid editionId, Uuid operationId,
        Func<CriteriaEdition, ActorAttribution, AccessAdmissionEvidence, long, Result> decide, CancellationToken ct)
        where TRequest : IRequestBase
    {
        if (editionId == Uuid.Empty || operationId == Uuid.Empty)
            return await Invalid().ConfigureAwait(false);
        if (RequestActor.IsSystem(context.Actor))
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "An authorized human operator is required."));
        if (authority is null || !await CriteriaAuthoringReadiness.CheckAsync(readiness, ct).ConfigureAwait(false))
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Source-authoritative operator admission is currently unavailable.", true));

        var observed = await aggregates.HydrateAsync(new CriteriaEdition(editionId, operationId), ct).ConfigureAwait(false);
        var generation = observed.NextExecutionGeneration;
        var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(canonical), generation);
        AccessAdmissionEvidence? evidence;
        try
        {
            evidence = await authority.AdmitCriteriaOperatorAsync(context, operationId, observed.Stream.ToString(),
                digest, ct).ConfigureAwait(false);
        }
        catch (AccessAdmissionUnavailableException)
        {
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Source-authoritative operator admission is currently unavailable.", true));
        }
        if (evidence is null)
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The current actor is not authorized to manage criteria editions."));
        if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(evidence))
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Source-authoritative operator admission is currently unavailable.", true));

        var actor = RbacOperationActor.From(context.Actor);
        if (!await CriteriaAuthoringReadiness.CheckAsync(readiness, ct).ConfigureAwait(false))
            return Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Criteria authoring is currently unavailable.", true));
        var outcome = await executor.ExecuteAsync(new CriteriaEdition(editionId, operationId), owner =>
            AggregateOutcome.CommitOnSuccess(decide(owner, actor, evidence, generation)), context, ct).ConfigureAwait(false);
        if (!outcome.IsSuccess)
            return Result<CriteriaOperationReceipt>.Failure(outcome.Error!);

        return await receipts.GetOperationAsync(editionId, operationId, null, ct).ConfigureAwait(false);
    }

    static ValueTask<Result<CriteriaOperationReceipt>> Invalid() => ValueTask.FromResult(
        Result<CriteriaOperationReceipt>.Failure(new RequestError(RequestErrorKind.Validation,
            "An edition, operation and valid label are required.")));
}
