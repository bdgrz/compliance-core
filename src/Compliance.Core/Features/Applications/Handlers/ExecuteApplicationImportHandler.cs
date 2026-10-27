using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

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
        var native = await verifier.LoadNativeAsync(request.TenantId.ToString(), request.BatchId, request.CommandId, ct)
            .ConfigureAwait(false);
        if (!native.IsSuccess)
            return Result.Failure(native.Error!);
        var batch = native.Value!.Batch;
        var plan = batch.Plan!;
        if (batch.IsCancelRequested || batch.IsEnded)
            return Result.Success;
        var source = await reader.HydrateAsync(new ApplicationImportSource(request.TenantId, plan.SourceKey,
            plan.SourceNamespace, selectedBatchId: batch.Id), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (source.Find(batch.Id) is not { IsActive: true, Admitted: { } admitted } ||
            admitted.Intent != plan.FrozenPlan || admitted.Operation.CausationEventId != plan.Metadata.EventId ||
            admitted.Operation.RootOperationId != plan.Operation.RootOperationId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The source claim of this batch is not visible yet.", isTransient: true));
        var provenance = ApplicationImportEffects.Child(request.CommandId, CommandKind, plan.Metadata.EventId,
            ApplicationImportIntentBindings.HeaderOf(plan.Operation), batch.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Decide(ApplicationImportBatch owner)
        {
            switch (owner.Acceptance.Decide(binding))
            {
                case NativeAcceptanceDecision.Replayed:
                    return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, owner.Acceptance.SelectedFact!));
                case NativeAcceptanceDecision.Conflict:
                    return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
            }
            return owner.Authorization is not null || owner.IsCancelRequested || owner.IsEnded
                ? SourceUseReceiverSupport.Superseded("The batch already authorized execution or ended.",
                    owner.Authorization is { } authorized ? NativeFactReference.Of(owner.Stream, authorized)
                        : ApplicationImportVerifier.Evidence(owner))
                : null;
        }
        var preparation = await NativeReceiverPreparation.PrepareAsync<ApplicationImportBatch, ApplicationImportContext>(reader,
            () => new ApplicationImportBatch(request.TenantId, batch.Id, request.CommandId), Decide,
            (owner, token) => verifier.PrepareAsync(new ApplicationImportNativeContext(request.TenantId, owner), token), ct)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known.IsSuccess ? Result.Success : Result.Failure(known.Error!);
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
                        var raised = fresh.AuthorizeExecution(provenance, plan.FrozenPlan);
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
