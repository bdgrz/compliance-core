using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>
///     Cancels the exact pending closure after an authoritative permanent proof mismatch and fences its delayed
///     completion in the same append. A closure that already closed the finding is an irreversible decision: the abort
///     is a terminal no-op and the closure recovers forward. A closure that is not visible yet is a transient wait.
/// </summary>
sealed class AbortFindingClosureHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AbortFindingClosure, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AbortFindingClosure> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.Generation < 1 ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The abort names another operation, generation or realm.");
        FindingClosureFailureProof proof;
        try
        {
            proof = await FindingContent.ReadAsync<FindingClosureFailureProof>(content, tenant, command.FailureProof, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return SourceUseReceiverSupport.Transient($"The failure proof is not available yet: {exception.Message}");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The failure proof cannot be verified: {exception.Message}");
        }

        if (command.FailureProof.Schema != FindingContent.FailureSchema || proof.ProgramId == Uuid.Empty ||
            proof.FindingId == Uuid.Empty || proof.RootOperationId != command.OperationId ||
            proof.Generation != command.Generation || string.IsNullOrWhiteSpace(proof.Reason))
            return SourceUseReceiverSupport.Permanent("The failure proof is not bound to this exact closure operation.");
        var stream = new FindingAggregate(tenant, proof.ProgramId, proof.FindingId).Stream;
        if (stream != command.Target)
            return SourceUseReceiverSupport.Permanent("The failure proof names another finding.");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var finding = await reader.HydrateAsync(new FindingAggregate(tenant, proof.ProgramId, proof.FindingId,
                    command.CommandId, command.OperationId), ct).ConfigureAwait(false);
                if (finding.AcceptedClosure(command.OperationId) is not { } accepted)
                    return SourceUseReceiverSupport.Transient("The accepted closure is not visible yet.");
                ClosureIntent intent;
                try
                {
                    intent = await FindingContent.ReadAsync<ClosureIntent>(content, tenant, accepted.Intent, ct)
                        .ConfigureAwait(false);
                }
                catch (ImmutableContentIntegrityException exception)
                {
                    return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                        $"The frozen closure cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
                }

                if (accepted.Generation != command.Generation || intent.Header.RootOperationId != command.OperationId ||
                    proof.Admission != NativeFactReference.Of(finding.Stream, accepted) || proof.Intent != accepted.Intent ||
                    intent.Header.FirstOwner != finding.Stream || intent.ParentKind != ClosureParentKind.Finding ||
                    intent.ParentId != proof.FindingId || intent.ParentGeneration != (ulong)command.Generation)
                    return SourceUseReceiverSupport.Permanent("The abort differs from the exact accepted closure proof.");
                if (proof.ActionId is { } actionId)
                {
                    IReadOnlyList<AdmittedChild> children;
                    try
                    {
                        children = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(content, tenant.ToString(),
                            intent.AdmittedChildren, ct).ConfigureAwait(false);
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                            $"The frozen admitted action set cannot be verified: {exception.Message}",
                            exception is ImmutableContentUnavailableException));
                    }
                    if (children.All(child => child.ChildId != actionId))
                        return SourceUseReceiverSupport.Permanent("The failure proof action is outside the frozen closure selection.");
                }
                var provenance = FindingClosureEffects.Child(command.CommandId, nameof(AbortFindingClosure),
                    accepted.Metadata.EventId, intent.Header, finding.Stream, clock.GetUtcNow());
                var binding = AcceptedOperationBinding.Of(provenance);
                if (Decide(finding, binding, command.OperationId) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new FindingAggregate(tenant, proof.ProgramId, proof.FindingId,
                    command.CommandId, command.OperationId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.CancelClosure(provenance, command.FailureProof);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                    fresh.TerminalClosure(command.OperationId)!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The finding changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>An exact replay, a closure that already closed the finding, or a closure canceled by another operation.</summary>
    static Result<ReceiverOutcome>? Decide(FindingAggregate finding, AcceptedOperationBinding binding, Uuid operationId)
    {
        switch (finding.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(finding.Stream,
                    finding.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return finding.TerminalClosure(operationId) switch
        {
            FindingClosedV2 closed => SourceUseReceiverSupport.Superseded(
                "The closure already closed the finding; it recovers forward.", NativeFactReference.Of(finding.Stream, closed)),
            FindingClosureCanceled canceled => SourceUseReceiverSupport.Replayed(NativeFactReference.Of(finding.Stream,
                canceled)),
            _ => null,
        };
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
