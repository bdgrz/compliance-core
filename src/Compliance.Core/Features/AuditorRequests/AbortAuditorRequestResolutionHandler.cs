using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Cancels the exact pending resolution after an authoritative permanent proof mismatch and fences its delayed
///     completion in the same append. A resolution that already resolved the request is an irreversible decision: the abort
///     is a terminal no-op and the resolution recovers forward. A resolution that is not visible yet is a transient wait.
/// </summary>
sealed class AbortAuditorRequestResolutionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AbortAuditorRequestResolution, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AbortAuditorRequestResolution> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.Generation < 1 ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The abort names another operation, generation or realm.");
        try
        {
            if (await AuditorResolutionReplay.ResolveAsync(events, command, ct).ConfigureAwait(false) is { } replay)
                return replay;
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact resolution receiver replay cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }

        if (command.FailureProof.Schema != AuditorContent.FailureSchema)
            return SourceUseReceiverSupport.Permanent("A new cancellation needs the fully bound failure proof schema.");
        AuditorRequestFailureProof proof;
        try
        {
            proof = await AuditorContent.ReadAsync<AuditorRequestFailureProof>(content, tenant, command.FailureProof, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return SourceUseReceiverSupport.Transient($"The failure proof cannot be verified: {exception.Message}");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The failure proof cannot be verified: {exception.Message}");
        }

        if (proof.ProgramId == Uuid.Empty || proof.RequestId == Uuid.Empty || proof.Admission is null ||
            proof.Intent is null || proof.RootOperationId != command.OperationId || proof.Generation != command.Generation ||
            proof.Admission.Stream != command.Target || string.IsNullOrWhiteSpace(proof.Reason))
            return SourceUseReceiverSupport.Permanent("The failure proof is not bound to this exact resolution admission.");
        var stream = new AuditorRequest(tenant, proof.ProgramId, proof.RequestId).Stream;
        if (stream != command.Target)
            return SourceUseReceiverSupport.Permanent("The failure proof names another request.");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var request = await reader.HydrateAsync(new AuditorRequest(tenant, proof.ProgramId, proof.RequestId,
                    command.CommandId, command.OperationId), ct).ConfigureAwait(false);
                if (request.AcceptedResolution(command.OperationId) is not { } accepted)
                    return SourceUseReceiverSupport.Transient("The accepted resolution is not visible yet.");
                ClosureIntent intent;
                try
                {
                    intent = await AuditorContent.ReadAsync<ClosureIntent>(content, tenant, accepted.Intent, ct)
                        .ConfigureAwait(false);
                }
                catch (ImmutableContentUnavailableException exception)
                {
                    return SourceUseReceiverSupport.Transient(
                        $"The frozen resolution cannot be verified: {exception.Message}");
                }
                catch (ImmutableContentIntegrityException exception)
                {
                    return SourceUseReceiverSupport.Permanent(
                        $"The frozen resolution cannot be verified: {exception.Message}");
                }

                var history = await AuditorRequestHistory.ReadAsync(events, request, ct).ConfigureAwait(false);
                var scoped = await AuditorCommands.TermsAsync(content, tenant, request, ct).ConfigureAwait(false);
                if (!scoped.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(scoped.Error!);
                var children = await AuditorRequestResolutionProofs.ChildrenAsync(content, tenant, intent, ct).ConfigureAwait(false);
                if (proof.Admission != NativeFactReference.Of(request.Stream, accepted) || proof.Intent != accepted.Intent ||
                    proof.ResponseId is { } responseId && children.All(child => child.Kind != AdmittedRequestChildKind.Response ||
                        child.ChildId != responseId) || history.AcceptedResolution(command.OperationId) is null ||
                    intent.Header.FirstOwner != request.Stream || intent.ParentId != request.Id ||
                    accepted.Generation != command.Generation || intent.Header.RootOperationId != command.OperationId)
                    return SourceUseReceiverSupport.Permanent("The abort differs from the accepted resolution.");
                var provenance = AuditorRequestResolutionEffects.Child(command.CommandId,
                    nameof(AbortAuditorRequestResolution), accepted.Metadata.EventId, intent.Header, request.Stream,
                    clock.GetUtcNow());
                var binding = AcceptedOperationBinding.Of(provenance);
                if (Decide(request, binding, command.OperationId) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new AuditorRequest(tenant, proof.ProgramId, proof.RequestId,
                    command.CommandId, command.OperationId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.CancelResolution(provenance, command.FailureProof);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream,
                                    fresh.TerminalResolution(command.OperationId)!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (ImmutableContentIntegrityException error)
            {
                return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The exact resolution cancellation proof cannot be verified: {error.Message}",
                    error is ImmutableContentUnavailableException));
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The request changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>An exact replay, a resolution that already resolved the request, or one canceled by another operation.</summary>
    static Result<ReceiverOutcome>? Decide(AuditorRequest request, AcceptedOperationBinding binding, Uuid operationId)
    {
        switch (request.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(request.Stream,
                    request.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return request.TerminalResolution(operationId) switch
        {
            AuditorRequestResolved resolved => SourceUseReceiverSupport.Superseded(
                "The resolution already resolved the request; it recovers forward.",
                NativeFactReference.Of(request.Stream, resolved)),
            AuditorRequestResolutionCanceled canceled => SourceUseReceiverSupport.Replayed(
                NativeFactReference.Of(request.Stream, canceled)),
            _ => null,
        };
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
