using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Authorizes execution of a sealed batch once its source is claimed: the batch records one
///     <see cref="ApplicationImportExecutionAuthorized" /> that drives the target reservations, the applications and
///     the publication. A canceled or ended batch authorizes nothing and the command is a terminal no-op; an exact
///     retry replays.
/// </summary>
sealed class ExecuteApplicationImportHandler(IAggregateReader reader, IAggregateExecutor executor,
    ApplicationImportVerifier verifier, ITenantActivity tenants, TimeProvider clock)
    : IRequestHandler<ExecuteApplicationImport>
{
    const int MaximumAttempts = 4;
    const string CommandKind = "ExecuteApplicationImport";

    public async ValueTask<Result> HandleAsync(IRequestContext<ExecuteApplicationImport> context, CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the trusted system actor may execute an import."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.BatchId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, tenant and batch are required."));
        if (!await tenants.IsActiveAsync(request.TenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The tenant is not active; retry after reactivation.", isTransient: true));
        var loaded = await verifier.LoadAsync(request.TenantId.ToString(), request.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result.Failure(loaded.Error!);
        var (batch, plan) = (loaded.Value!.Batch, loaded.Value.Plan);
        if (batch.IsCancelRequested || batch.IsEnded)
            return Result.Success;
        var source = await reader.HydrateAsync(new ApplicationImportSource(request.TenantId, plan.SourceKey,
            plan.SourceNamespace, selectedBatchId: batch.Id), ct).ConfigureAwait(false);
        if (source.Find(batch.Id) is not { IsActive: true, Admitted: { } admitted } ||
            admitted.Intent != loaded.Value.PlanRef || admitted.Operation.CausationEventId != batch.Plan!.Metadata.EventId ||
            admitted.Operation.RootOperationId != plan.Header.RootOperationId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The source claim of this batch is not visible yet.", isTransient: true));

        var provenance = ApplicationImportEffects.Child(request.CommandId, CommandKind, batch.Plan!.Metadata!.EventId,
            plan, batch.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await executor.ExecuteAsync(new ApplicationImportBatch(request.TenantId, batch.Id,
                    request.CommandId), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result.Success);
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.Authorization is not null || fresh.IsCancelRequested || fresh.IsEnded)
                            return AggregateOutcome.Discard(Result.Success);
                        var raised = fresh.AuthorizeExecution(provenance, loaded.Value.PlanRef);
                        return raised.IsSuccess ? AggregateOutcome.Commit(raised) : AggregateOutcome.Discard(raised);
                    }, context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The batch changed concurrently; rehydrate and decide again, including against a cancellation.
            }
        }
    }
}
