using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Materializes one admitted response draft on its own owner at revision one. The frozen admission intent is read from
///     verified content and the admission that carries exactly that intent is found on the authoritative request; nothing
///     a command states is trusted until its owner confirms it. An exact replay returns the original fact, an admission
///     that is not visible yet is transient, a changed intent is a permanent conflict, and the request is never loaded for
///     live state, so a request that already resolved does not lose an admitted response.
/// </summary>
sealed class MaterializeAuditorResponseHandler(IAggregateReader reader, IAggregateExecutor executor, IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<MaterializeAuditorResponse, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<MaterializeAuditorResponse> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.RequestId == Uuid.Empty ||
            command.ResponseId == Uuid.Empty || command.ExpectedRevision is not (null or 0) ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The materialization names another operation, response or realm.");

        try
        {
            if (await AuditorChildMaterializationReplay.ResolveAsync(events, command.Source, command.Target, tenant,
                command.RequestId, command.ResponseId, command.CommandId, command.OperationId, command.Generation,
                command.Intent, AdmittedRequestChildKind.Response, ct).ConfigureAwait(false) is { } replay)
                return replay;
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact child materialization cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }

        ChildAdmissionIntent intent;
        AuditorResponseDraftContent authored;
        try
        {
            intent = await AuditorContent.ReadAsync<ChildAdmissionIntent>(content, tenant, command.Intent, ct)
                .ConfigureAwait(false);
            authored = await AuditorContent.ReadAsync<AuditorResponseDraftContent>(content, tenant,
                intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return SourceUseReceiverSupport.Transient($"The frozen admission cannot be verified: {exception.Message}");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen admission cannot be verified: {exception.Message}");
        }

        var target = new AuditorResponse(tenant, authored.ProgramId, command.RequestId, command.ResponseId).Stream;
        var parent = new AuditorRequest(tenant, authored.ProgramId, command.RequestId).Stream;
        if (intent.ChildKind != AdmittedChildKind.AuditorResponse || intent.ChildId != command.ResponseId ||
            intent.ParentId != command.RequestId || intent.Header.RootOperationId != command.OperationId ||
            authored.ResponseId != command.ResponseId || authored.RequestId != command.RequestId ||
            command.Target != target || command.Source.Stream != parent || command.Generation != (long)intent.ParentGeneration)
            return SourceUseReceiverSupport.Permanent("The materialization differs from the frozen admission.");

        var admitted = await AdmittedChildVerifier.VerifyAsync<AuditorResponseAdmitted>(reader, events, content, tenant,
            authored.ProgramId, command.RequestId, command.Source, command.Intent, command.OperationId,
            command.ResponseId, fact => fact.Intent, ct).ConfigureAwait(false);
        if (!admitted.IsSuccess)
            return Result<ReceiverOutcome>.Failure(admitted.Error!);
        if (!AdmittedChildVerifier.HeaderMatches(admitted.Value!.Operation, intent.Header) ||
            admitted.Value.Generation != (long)intent.ParentGeneration || (ulong)admitted.Value.Revision != intent.ParentAdmissionRevision)
            return SourceUseReceiverSupport.Permanent("The frozen header differs from the exact native admission.");
        if (authored.AuthorMemberId.ToString() != intent.Header.OriginalActor.Id || authored.At != intent.Header.AcceptedAt)
            return SourceUseReceiverSupport.Permanent("The admitted draft differs from its native author or acceptance time.");

        var provenance = AuditorRequestResolutionEffects.Child(command.CommandId, nameof(MaterializeAuditorResponse),
            command.Source.EventId, intent.Header, target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var result = await AuditorContent.UnitResultAsync(content, tenant, command.OperationId, ct).ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(Open(tenant, authored.ProgramId, command), ct).ConfigureAwait(false);
                if (Decide(current, binding) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(Open(tenant, authored.ProgramId, command), owner =>
                    {
                        if (Decide(owner, binding) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = owner.Materialize(provenance, command.Intent, authored.AuthorMemberId,
                            intent.Header.AcceptedAt, result);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(owner.Stream, owner.Created!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The response changed concurrently; rehydrate and decide again.
            }
        }
    }

    static AuditorResponse Open(Uuid tenant, Uuid program, MaterializeAuditorResponse command) =>
        new(tenant, program, command.RequestId, command.ResponseId, command.CommandId);

    /// <summary>An exact replay of this receiver command, or the permanent conflict of a response materialized otherwise.</summary>
    static Result<ReceiverOutcome>? Decide(AuditorResponse owner, AcceptedOperationBinding binding)
    {
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return owner.IsMaterialized
            ? SourceUseReceiverSupport.Permanent("The auditor response was already materialized by another operation.")
            : null;
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
