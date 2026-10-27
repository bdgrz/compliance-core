using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Independence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Appends <see cref="FindingClosedV2" /> once every admitted action is held under this closure's grant. The held
///     completion proofs are re-derived from the authoritative actions and must equal the manifest the command carries;
///     the exact waiver proof, when one was required, is verified again at the declared decision instant. A closure that
///     was canceled is a terminal no-op, a waiver that no longer verifies is a permanent mismatch that aborts the
///     closure, and a grant that is not visible yet is a transient wait. The closed finding is appended before any use
///     is settled.
/// </summary>
sealed class CompleteFindingClosureHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, ISeparationOfDutiesWaiverProofResolver waivers, FindingClosureVerifier verifier,
    FindingClosureFences fences, FindingReader findings, TimeProvider clock)
    : IRequestHandler<CompleteFindingClosure, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CompleteFindingClosure> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty)
            return SourceUseReceiverSupport.Permanent("The completion names another operation.");
        var closure = await verifier.VerifyAcceptedAsync(command.Source, command.Intent, command.OperationId,
            command.CommandId, ct).ConfigureAwait(false);
        if (closure.Error is not null)
            return Result<ReceiverOutcome>.Failure(closure.Error);
        var (finding, intent, decision, tenant) = (closure.Finding!, closure.Intent!, closure.Decision!, closure.TenantId);
        if (command.Target != finding.Stream || command.FindingId != intent.ParentId ||
            command.Generation != (long)intent.ParentGeneration)
            return SourceUseReceiverSupport.Permanent("The completion differs from the frozen closure intent.");

        var now = clock.GetUtcNow();
        var provenance = FindingClosureEffects.Child(command.CommandId, nameof(CompleteFindingClosure),
            command.Source.EventId, intent.Header, finding.Stream, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        if (Decide(finding, binding, command.OperationId) is { } early)
            return early;

        var held = await FindingClosureProofs.HeldAsync(reader, content, finding, intent, decision, tenant,
            command.CommandId, ct).ConfigureAwait(false);
        if (!held.IsSuccess)
            return Result<ReceiverOutcome>.Failure(held.Error!);
        var proofs = await FindingContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            FindingContent.ProofsSchema, held.Value!, ct).ConfigureAwait(false);
        if (proofs != command.ActionProofManifest)
            return SourceUseReceiverSupport.Permanent(
                "The held completion proofs differ from the manifest the closure was completed with.");

        if (intent.WaiverProof is { } waiverReference)
        {
            SeparationOfDutiesWaiverProof proof;
            try
            {
                proof = await FindingContent.ReadAsync<SeparationOfDutiesWaiverProof>(content, tenant, waiverReference, ct)
                    .ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The frozen waiver proof cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
            }

            var verified = await waivers.VerifyAsync(proof, intent.Header.AcceptedAt, ct).ConfigureAwait(false);
            if (!verified.IsSuccess)
                return verified.Error!.IsTransient
                    ? Result<ReceiverOutcome>.Failure(verified.Error)
                    : await fences.AbortAsync(closure, command.Source, command.Intent,
                        "The separation-of-duties waiver proof no longer verifies.", null, context, ct).ConfigureAwait(false);
        }

        var view = new FindingClosureView(decision.DecisionId, decision.VerificationRationale, decision.ResolutionEvidence,
            decision.Rationale, decision.CloserMemberId, decision.ClosedBy, decision.DecidedAt,
            decision.SeparationOfDutiesWaiverId);
        var current = await findings.ComposeAsync(finding, now, ct).ConfigureAwait(false);
        var after = FindingViews.WithClosure(current!, view, finding.Revision + 1, now);
        var closureContent = await FindingContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            FindingContent.ClosureSchema, view, ct).ConfigureAwait(false);
        var (_, result) = await FindingContent.FreezeAsync(content, tenant, FindingContent.ViewSchema, after, ct)
            .ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new FindingAggregate(tenant, decision.ProgramId, finding.FindingId,
                    command.CommandId, command.OperationId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.CompleteClosure(provenance, closureContent, proofs, command.Intent, result);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                    fresh.Closed!)))))
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

    /// <summary>An exact replay, or a closure that was canceled and so can never close the finding.</summary>
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
            FindingClosureCanceled canceled => SourceUseReceiverSupport.Superseded(
                "The closure was canceled; its fence stands.", NativeFactReference.Of(finding.Stream, canceled)),
            FindingClosedV2 => SourceUseReceiverSupport.Permanent("The finding was closed by another operation."),
            _ => null,
        };
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
