using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Evaluations.Handlers;

/// <summary>
///     Records a minor deviation's disposition on that evaluation's own stream; a waiver must be approved, active,
///     and scoped to the deviation and round. An exact retry returns the original view after later work.
/// </summary>
public sealed class DisposeControlEvaluationDeviationHandler(NativeOperationExecutor operations,
    ControlEvaluationReader evaluations, IAggregateReader reader, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<DisposeControlEvaluationDeviation, ControlEvaluationView>
{
    const string CommandKind = "DisposeControlEvaluationDeviation";

    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<DisposeControlEvaluationDeviation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.EvaluationId == Uuid.Empty || request.DeviationId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return Result<ControlEvaluationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A disposition requires a command id, an evaluation, a deviation and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        return await EvaluationWork.RunAsync(operations, evaluations, content, context, request.TenantId,
            request.ProgramId, request.ControlId, request.EvaluationId, request.CommandId, CommandKind,
            AuthoredIntentDigest.Compute(request), request.ExpectedRevision, actor, now, async (state, token) =>
            {
                SeparationOfDutiesWaiver? waiver = null;
                if (request.WaiverId is { } waiverId)
                    waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), token)
                        .ConfigureAwait(false);
                var failure = ControlEvaluationRules.CheckDisposition(state, request, actor.MemberId, now, waiver);
                if (failure is not null)
                    return Result<EvaluationChange>.Failure(failure.ToRequestError());
                var after = state.Clone();
                after.DisposeDeviation(state.Revision + 1, request.DeviationId, request.Disposition,
                    request.Rationale.Trim(), waiver?.Id);
                return Result<EvaluationChange>.Success(new EvaluationChange(after,
                    (ControlEvaluation owner) => owner.PrepareWork(request.ExpectedRevision),
                    (ControlEvaluation owner, OperationProvenance provenance, ImmutableContentRef first,
                        ImmutableContentRef operationResult) => owner.DisposeDeviation(provenance,
                        request.DeviationId, request.Disposition, request.Rationale, waiver?.Id, first,
                        operationResult)));
            }, ct).ConfigureAwait(false);
    }
}
