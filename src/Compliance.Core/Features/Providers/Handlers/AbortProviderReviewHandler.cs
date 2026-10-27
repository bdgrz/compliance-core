using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Cancels the exact pending review after an authoritative permanent proof mismatch, before the irreversible
///     recording. The failure proof is verified content. A review that is already recorded recovers forward and the
///     receiver is a terminal no-op; an exact retry replays the original cancellation.
/// </summary>
sealed class AbortProviderReviewHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<AbortProviderReview, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AbortProviderReview> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.OperationId == Uuid.Empty ||
            request.Generation != ProviderReviewEffects.Generation ||
            !Uuid.TryParse(request.Target.Realm, out var tenantId) || tenantId == Uuid.Empty ||
            request.Target != ProviderAssuranceStreams.Review(tenantId, request.OperationId))
            return SourceUseReceiverSupport.Permanent("The abort names another review, tenant or generation.");
        try
        {
            _ = await ProviderAssuranceContent.ReadAsync<ProviderReviewFailureProof>(content, tenantId,
                request.FailureProof, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The failure proof cannot be verified: {exception.Message}");
        }
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var owner = await reader.HydrateAsync(new ProviderAssuranceReview(tenantId, request.OperationId,
                    request.CommandId), ct).ConfigureAwait(false);
                if (owner.Requested is not { } requested)
                    return SourceUseReceiverSupport.Transient("The admitted review is not visible yet.");
                var header = await ProviderAssuranceContent.ReadAsync<ProviderReviewIntent>(content, tenantId,
                    requested.Intent, ct).ConfigureAwait(false);
                var provenance = SourceUseReceiverSupport.Child(request.CommandId, nameof(AbortProviderReview),
                    requested.Metadata.EventId, header.Header, owner.Stream, clock.GetUtcNow());
                var binding = AcceptedOperationBinding.Of(provenance);
                if (Decide(owner, binding) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new ProviderAssuranceReview(tenantId, request.OperationId,
                    request.CommandId), fresh =>
                {
                    if (Decide(fresh, binding) is { } decided)
                        return AggregateOutcome.Discard(Result<Decision>.Success(new Decision(decided)));
                    var raised = fresh.Cancel(provenance, request.FailureProof);
                    return raised.IsSuccess
                        ? AggregateOutcome.Commit(Result<Decision>.Success(new Decision(
                            SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                fresh.Acceptance.SelectedFact!)))))
                        : AggregateOutcome.Discard(Result<Decision>.Failure(raised.Error!));
                }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The review changed concurrently; rehydrate and decide again.
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return SourceUseReceiverSupport.Permanent($"The frozen review cannot be verified: {exception.Message}");
            }
        }
    }

    static Result<ReceiverOutcome>? Decide(ProviderAssuranceReview owner, AcceptedOperationBinding binding)
    {
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream,
                    owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }
        return owner.Recorded is { } recorded
            ? SourceUseReceiverSupport.Superseded("The review is already recorded; it recovers forward.",
                NativeFactReference.Of(owner.Stream, recorded))
            : null;
    }

    sealed record Decision(Result<ReceiverOutcome> Outcome);
}
