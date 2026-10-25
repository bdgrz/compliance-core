using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Accepts an immutable plan version; exact retries resolve before mutable control guards and retain the original result.</summary>
public sealed class DefineControlEvaluationPlanHandler(NativeOperationExecutor operations,
    ControlReader controlSources, OperatingAuthority authority, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<DefineControlEvaluationPlan, ControlEvaluationPlanVersionView>
{
    public ValueTask<Result<ControlEvaluationPlanVersionView>> HandleAsync(
        IRequestContext<DefineControlEvaluationPlan> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlEvaluationPlanVersionView>> HandleCoreAsync(
        IRequestContext<DefineControlEvaluationPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ExpectedVersion < 0 || request.ControlId == Uuid.Empty || request.ProgramId == Uuid.Empty)
            return Result<ControlEvaluationPlanVersionView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A plan definition requires its owner and a non-negative original version."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        if (!await authority.ManagesProgramAsync(request.TenantId, actor, request.ProgramId, ct).ConfigureAwait(false))
            return Result<ControlEvaluationPlanVersionView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a program manager may define a control evaluation plan."));
        var now = clock.GetUtcNow();
        var logicalId = ControlEvaluationPlan.OperationId(request.TenantId, request.ProgramId, request.ControlId,
            request.ExpectedVersion);
        var stream = new ControlEvaluationPlan(request.TenantId, request.ProgramId, request.ControlId).Stream;
        var provenance = RootOperation.Provenance(logicalId, nameof(DefineControlEvaluationPlan), stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), request.ExpectedVersion,
            AuthoredIntentDigest.Compute(request), now) with
        { FirstOwner = stream };
        ImmutableContentRef? body = null;
        ImmutableContentRef? result = null;

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(ControlEvaluationPlan owner, CancellationToken token)
        {
            try
            {
                return await PrepareCoreAsync(owner, token).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException error)
            {
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The exact native preparation content cannot be verified.", error is ImmutableContentUnavailableException));
            }
        }

        async ValueTask<Result<FirstResponseDescriptor>> PrepareCoreAsync(ControlEvaluationPlan owner, CancellationToken token)
        {
            var prepared = owner.PrepareDefine(request.ExpectedVersion);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            var control = await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId,
                request.ProgramId, request.ControlId, token).ConfigureAwait(false);
            if (control is null)
                return Result<FirstResponseDescriptor>.Failure(ControlOperationsSource.ControlNotFound());
            if (!control.IsApproved || control.IsRetired)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "An evaluation plan requires a non-retired control with an approved version."));
            if (!control.ReadVersions().Any(version => version.VersionId == request.ControlVersionId))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The approved control version was not found."));
            var rules = new ControlEvaluationPlanRules(request.TenantId, request.ProgramId, request.ControlId);
            if (rules.Define(0, request.ControlVersionId, logicalId, request.Objective, request.Steps,
                    request.TesterIndependenceRequired, actor.MemberId, provenance.Actor, now) is { } invalid)
                return Result<FirstResponseDescriptor>.Failure(invalid.ToRequestError());
            var version = rules.CurrentVersion! with { Version = owner.Revision + 1 };
            body = await NativeContent.WriteAsync(content, request.TenantId, ControlEvaluationPlanReader.VersionSchema,
                version, token).ConfigureAwait(false);
            var frozen = await NativeContent.FreezeAsync(content, request.TenantId,
                ControlEvaluationPlanReader.VersionSchema, version, token).ConfigureAwait(false);
            result = frozen.Result;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlEvaluationPlan>(AcceptedOperationBinding.Of(provenance),
            () => new ControlEvaluationPlan(request.TenantId, request.ProgramId, request.ControlId, logicalId),
            owner => owner.Acceptance, PrepareAsync,
            (ControlEvaluationPlan owner, ImmutableContentRef first) => owner.Define(provenance, request.ControlVersionId,
                body!, first, result!)), context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<ControlEvaluationPlanVersionView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
