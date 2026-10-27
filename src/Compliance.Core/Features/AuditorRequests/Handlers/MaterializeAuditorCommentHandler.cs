using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Materializes one admitted comment as its own thread at revision one. The exact body, compartment and original
///     actor come from the frozen admission, which is confirmed on the authoritative request; a request that resolved
///     since does not discard the comment. An exact replay returns the original fact.
/// </summary>
sealed class MaterializeAuditorCommentHandler(IAggregateReader reader, IAggregateExecutor executor, IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<MaterializeAuditorComment, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<MaterializeAuditorComment> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.RequestId == Uuid.Empty ||
            command.CommentId == Uuid.Empty || command.ExpectedRevision is not (null or 0) ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The materialization names another operation, comment or realm.");

        try
        {
            if (await AuditorChildMaterializationReplay.ResolveAsync(events, command.Source, command.Target, tenant,
                command.RequestId, command.CommentId, command.CommandId, command.OperationId, command.Generation,
                command.Intent, AdmittedRequestChildKind.Comment, ct).ConfigureAwait(false) is { } replay)
                return replay;
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact child materialization cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }

        ChildAdmissionIntent intent;
        AuditorCommentContent authored;
        try
        {
            intent = await AuditorContent.ReadAsync<ChildAdmissionIntent>(content, tenant, command.Intent, ct)
                .ConfigureAwait(false);
            authored = await AuditorContent.ReadAsync<AuditorCommentContent>(content, tenant,
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

        var target = new AuditorFeedbackThread(tenant, authored.ProgramId, command.RequestId, command.CommentId).Stream;
        var parent = new AuditorRequest(tenant, authored.ProgramId, command.RequestId).Stream;
        if (intent.ChildKind != AdmittedChildKind.AuditorComment || intent.ChildId != command.CommentId ||
            intent.ParentId != command.RequestId || intent.Header.RootOperationId != command.OperationId ||
            authored.CommentId != command.CommentId || authored.RequestId != command.RequestId ||
            command.Target != target || command.Source.Stream != parent || command.Generation != (long)intent.ParentGeneration)
            return SourceUseReceiverSupport.Permanent("The materialization differs from the frozen admission.");

        var admitted = await AdmittedChildVerifier.VerifyAsync<AuditorCommentAdmitted>(reader, events, content, tenant,
            authored.ProgramId, command.RequestId, command.Source, command.Intent, command.OperationId,
            command.CommentId, fact => fact.Intent, ct).ConfigureAwait(false);
        if (!admitted.IsSuccess)
            return Result<ReceiverOutcome>.Failure(admitted.Error!);
        if (!AdmittedChildVerifier.HeaderMatches(admitted.Value!.Operation, intent.Header) ||
            admitted.Value.Generation != (long)intent.ParentGeneration || (ulong)admitted.Value.Revision != intent.ParentAdmissionRevision)
            return SourceUseReceiverSupport.Permanent("The frozen header differs from the exact native admission.");
        if (authored.ActorMemberId.ToString() != intent.Header.OriginalActor.Id || authored.At != intent.Header.AcceptedAt)
            return SourceUseReceiverSupport.Permanent("The admitted comment differs from its native author or acceptance time.");

        var provenance = AuditorRequestResolutionEffects.Child(command.CommandId, nameof(MaterializeAuditorComment),
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
                        var raised = owner.Materialize(provenance, command.Intent, authored.ActorMemberId,
                            intent.Header.AcceptedAt, result);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(owner.Stream, owner.Recorded!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The thread changed concurrently; rehydrate and decide again.
            }
        }
    }

    static AuditorFeedbackThread Open(Uuid tenant, Uuid program, MaterializeAuditorComment command) =>
        new(tenant, program, command.RequestId, command.CommentId, command.CommandId);

    /// <summary>An exact replay of this receiver command, or the permanent conflict of a thread materialized otherwise.</summary>
    static Result<ReceiverOutcome>? Decide(AuditorFeedbackThread owner, AcceptedOperationBinding binding)
    {
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return owner.IsMaterialized
            ? SourceUseReceiverSupport.Permanent("The comment thread was already materialized by another operation.")
            : null;
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
