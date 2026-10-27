using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Appends <see cref="AuditorRequestResolved" /> once every admitted comment and sample selection is materialized and
///     every response is held under this resolution's grant with an accepted external disposition. The held proofs are
///     re-derived from the authoritative responses and must equal the manifest the command carries. A resolution that was
///     canceled is a terminal no-op, and a grant or child that is not visible yet is a transient wait. The resolved
///     request is appended before any use is settled.
/// </summary>
sealed class CompleteAuditorRequestResolutionHandler(IAggregateReader reader, IAggregateExecutor executor,
    IDomainEventReader events,
    IImmutableContentStore content, AuditorRequestResolutionVerifier verifier, TimeProvider clock)
    : IRequestHandler<CompleteAuditorRequestResolution, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CompleteAuditorRequestResolution> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty)
            return SourceUseReceiverSupport.Permanent("The completion names another operation.");
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

        var resolution = await verifier.VerifyAcceptedAsync(command.Source, command.Intent, command.OperationId,
            command.CommandId, ct).ConfigureAwait(false);
        if (resolution.Error is not null)
            return Result<ReceiverOutcome>.Failure(resolution.Error);
        var (auditorRequest, intent, decision, tenant) = (resolution.Request!, resolution.Intent!, resolution.Decision!,
            resolution.TenantId);
        if (command.Target != auditorRequest.Stream || command.RequestId != intent.ParentId ||
            command.Generation != (long)intent.ParentGeneration)
            return SourceUseReceiverSupport.Permanent("The completion differs from the frozen resolution intent.");

        var provenance = AuditorRequestResolutionEffects.Child(command.CommandId, nameof(CompleteAuditorRequestResolution),
            command.Source.EventId, intent.Header, auditorRequest.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        if (Decide(auditorRequest, binding, command.OperationId) is { } early)
            return early;

        IReadOnlyList<AdmittedRequestChild> children;
        try
        {
            children = await AuditorRequestResolutionProofs.ChildrenAsync(content, tenant, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return SourceUseReceiverSupport.Transient($"The admitted child manifest cannot be verified: {exception.Message}");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The admitted child manifest cannot be verified: {exception.Message}");
        }

        var drained = await AuditorRequestResolutionProofs.DrainedAsync(reader, children, tenant, decision.ProgramId,
            auditorRequest.RequestId, ct).ConfigureAwait(false);
        if (!drained.IsSuccess)
            return Result<ReceiverOutcome>.Failure(drained.Error!);
        var held = await AuditorRequestResolutionProofs.HeldAsync(reader, children, intent, decision, tenant,
            auditorRequest.RequestId, command.CommandId, ct).ConfigureAwait(false);
        if (!held.IsSuccess)
            return Result<ReceiverOutcome>.Failure(held.Error!);
        var proofs = await AuditorContent.WriteDeterministicAsync(content, tenant, command.CommandId,
            AuditorContent.ProofsSchema, held.Value!, ct).ConfigureAwait(false);
        if (proofs != command.ResponseProofManifest)
            return SourceUseReceiverSupport.Permanent(
                "The held disposition proofs differ from the manifest the resolution was completed with.");

        var result = await AuditorContent.UnitResultAsync(content, tenant, command.OperationId, ct).ConfigureAwait(false);
        var resolver = Uuid.Parse(intent.Header.OriginalActor.Id, CultureInfo.InvariantCulture);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new AuditorRequest(tenant, decision.ProgramId,
                    auditorRequest.RequestId, command.CommandId, command.OperationId), fresh =>
                    {
                        if (Decide(fresh, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = fresh.CompleteResolution(provenance, resolver, command.Intent, proofs, result);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(fresh.Stream, fresh.Resolved!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The request changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>An exact replay, or a resolution that was canceled and so can never resolve the request.</summary>
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
            AuditorRequestResolutionCanceled canceled => SourceUseReceiverSupport.Superseded(
                "The resolution was canceled; its fence stands.", NativeFactReference.Of(request.Stream, canceled)),
            AuditorRequestResolved => SourceUseReceiverSupport.Permanent("The request was resolved by another operation."),
            _ => null,
        };
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
