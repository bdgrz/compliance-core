using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Materializes one admitted corrective action on its own owner at revision one. The frozen admission intent is read
///     from verified content and the admission that carries exactly that intent is found on the authoritative finding;
///     nothing a command states is trusted until its owner confirms it. An exact replay returns the original fact. An
///     admission that is not visible yet is transient, a changed intent is a permanent conflict, and the action content
///     is the admitted terms with the original actor and the admission instant, so the finding is never loaded again.
/// </summary>
sealed class MaterializeCorrectiveActionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<MaterializeCorrectiveAction, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<MaterializeCorrectiveAction> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.ActionId == Uuid.Empty ||
            command.FindingId == Uuid.Empty || command.ExpectedRevision is not (null or 0) ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The materialization names another operation, action or realm.");

        ChildAdmissionIntent intent;
        AddCorrectiveAction authored;
        try
        {
            intent = await FindingContent.ReadAsync<ChildAdmissionIntent>(content, tenant, command.Intent, ct)
                .ConfigureAwait(false);
            authored = await FindingContent.ReadAsync<AddCorrectiveAction>(content, tenant,
                intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return SourceUseReceiverSupport.Transient($"The frozen admission is not available yet: {exception.Message}");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen admission cannot be verified: {exception.Message}");
        }

        var target = new CorrectiveAction(tenant, authored.ProgramId, command.FindingId, command.ActionId).Stream;
        var finding = new FindingAggregate(tenant, authored.ProgramId, command.FindingId).Stream;
        if (intent.ChildKind != AdmittedChildKind.CorrectiveAction || intent.ChildId != command.ActionId ||
            intent.ParentId != command.FindingId || intent.Header.RootOperationId != command.OperationId ||
            authored.ActionId != command.ActionId || authored.TenantId != tenant || authored.FindingId != command.FindingId ||
            command.Target != target || command.Source.Stream != finding || command.Generation != (long)intent.ParentGeneration)
            return SourceUseReceiverSupport.Permanent("The materialization differs from the frozen admission.");

        var parent = await reader.HydrateAsync(new FindingAggregate(tenant, authored.ProgramId, command.FindingId), ct)
            .ConfigureAwait(false);
        if (parent.Facts.FirstOrDefault(fact => fact.Metadata?.EventId == command.Source.EventId) is not CorrectiveActionAdmitted admitted)
            return SourceUseReceiverSupport.Transient("The action admission is not visible on its finding yet.");
        if (admitted.Metadata.AggregateVersion != command.Source.AggregateVersion || admitted.Intent != command.Intent ||
            admitted.Operation.LogicalId != command.ActionId || admitted.Operation.RootOperationId != command.OperationId)
            return SourceUseReceiverSupport.Permanent("The source differs from the admission of this action.");

        var view = new CorrectiveActionView(command.ActionId, authored.Description.Trim(), authored.OwnerMemberId,
            authored.DueOn, FindingStates.Open, intent.Header.OriginalActor, intent.Header.AcceptedAt, Revision: 1);
        var actionContent = await FindingContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            FindingContent.ActionSchema, view, ct).ConfigureAwait(false);
        var provenance = FindingClosureEffects.Child(command.CommandId, nameof(MaterializeCorrectiveAction),
            command.Source.EventId, intent.Header, target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new CorrectiveAction(tenant, authored.ProgramId,
                    command.FindingId, command.ActionId, command.CommandId), ct).ConfigureAwait(false);
                if (Decide(current, binding) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new CorrectiveAction(tenant, authored.ProgramId, command.FindingId,
                    command.ActionId, command.CommandId), action =>
                    {
                        if (Decide(action, binding) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = action.Materialize(provenance, actionContent, command.Intent);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(action.Stream, action.Added!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The action changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>An exact replay of this receiver command, or the permanent conflict of an action materialized otherwise.</summary>
    static Result<ReceiverOutcome>? Decide(CorrectiveAction action, AcceptedOperationBinding binding)
    {
        switch (action.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(action.Stream, action.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return action.IsMaterialized
            ? SourceUseReceiverSupport.Permanent("The corrective action was already materialized by another operation.")
            : null;
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
