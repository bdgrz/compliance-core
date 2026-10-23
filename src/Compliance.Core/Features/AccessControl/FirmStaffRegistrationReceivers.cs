using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The three receivers of <c>FirmStaffRegistrationProcess</c>. Each verifies the accepted registration from the
///     authoritative staff stream, resolves an exact replay from the target's accepted facts, and appends one fact. A
///     transient wait is a transient conflict; a changed intent or invalid proof is a permanent conflict.
/// </summary>
sealed class FirmStaffRegistrationReceivers(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> ClaimAsync(IExecutionContext context, ClaimFirmStaffIdentity command,
        CancellationToken ct)
    {
        var claimStream = new FirmStaffIdentityClaim(command.UserId).Stream;
        if (command.CommandId == Uuid.Empty || command.StaffMemberId == Uuid.Empty || command.UserId == Uuid.Empty ||
            command.Target != claimStream || command.Generation != FirmStaffEffects.Generation ||
            command.OperationId != command.StaffMemberId)
            return Permanent("The claim names another canonical user stream, generation or operation.");
        var verified = await VerifyAsync(command.StaffMemberId, command.Source, command.Intent, command.UserId,
            command.CommandId, ct).ConfigureAwait(false);
        if (verified.Failure is not null)
            return verified.Failure.Value;
        if (verified.Record!.IsRejected)
            return Superseded("The registration was rejected.", null);

        var intent = verified.Intent!;
        var provenance = FirmStaffEffects.Child(command.CommandId, nameof(ClaimFirmStaffIdentity),
            command.Source.EventId, intent, claimStream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new FirmStaffIdentityClaim(command.UserId, command.CommandId),
                    claim =>
                    {
                        switch (claim.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(Fact(claim.Stream, claim.Acceptance.SelectedFact!), false, null)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (claim.Claimed is { } existing)
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                claim.ClaimedByStaffMemberId == command.StaffMemberId
                                    ? new Step(Fact(claim.Stream, existing), false, null)
                                    : new Step(null, false, existing)));
                        var claimed = claim.Claim(provenance, command.Intent);
                        return claimed.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(
                                new Step(Fact(claim.Stream, claim.Acceptance.SelectedFact!), true, null)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(claimed.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Foreign is not null)
                    return await RejectForeignClaimAsync(context, command, intent, ct).ConfigureAwait(false);
                return Result<ReceiverOutcome>.Success(step.Value.Applied
                    ? new ReceiverOutcome.Applied(step.Value.Fact!)
                    : new ReceiverOutcome.Replayed(step.Value.Fact!));
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent claimant won the append; rehydrate and decide again.
            }
        }
    }

    public async ValueTask<Result<ReceiverOutcome>> CompleteAsync(IExecutionContext context,
        CompleteFirmStaffRegistration command, CancellationToken ct)
    {
        var recordStream = new FirmStaffRecord(command.StaffMemberId).Stream;
        if (command.CommandId == Uuid.Empty || command.StaffMemberId == Uuid.Empty || command.UserId == Uuid.Empty ||
            command.Target != recordStream || command.Generation != FirmStaffEffects.Generation ||
            command.OperationId != command.StaffMemberId)
            return Permanent("The completion names another staff stream, generation or operation.");
        var verified = await VerifyAsync(command.StaffMemberId, command.Source, command.Intent, command.UserId,
            command.CommandId, ct).ConfigureAwait(false);
        if (verified.Failure is not null)
            return verified.Failure.Value;
        if (verified.Record!.IsRejected)
            return Superseded("The registration was rejected.", null);

        var intent = verified.Intent!;
        var claim = await reader.HydrateAsync(new FirmStaffIdentityClaim(command.UserId), ct).ConfigureAwait(false);
        if (!claim.IsClaimed)
            return Transient("The permanent canonical-user claim is not visible yet.");
        if (claim.ClaimedByStaffMemberId != command.StaffMemberId)
            return Permanent("The canonical user is claimed by another staff record.");

        var provenance = FirmStaffEffects.Child(command.CommandId, nameof(CompleteFirmStaffRegistration),
            command.Source.EventId, intent, recordStream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var staff = await FirmStaffContent.WriteStaffAsync(content,
            Uuid.CreateVersion5(command.CommandId, "staff-content"), FirmStaffContent.RegisteredView(intent), ct)
            .ConfigureAwait(false);
        var result = await FirmStaffContent.WriteResultAsync(content,
            Uuid.CreateVersion5(command.CommandId, "operation-result"), staff, ct).ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new FirmStaffRecord(command.StaffMemberId, command.CommandId),
                    record =>
                    {
                        switch (record.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(Fact(record.Stream, record.Acceptance.SelectedFact!), false, null)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (record.IsRejected)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(null, false, null)));
                        var completed = record.CompleteRegistration(provenance, staff, result);
                        return completed.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(
                                new Step(Fact(record.Stream, record.Acceptance.SelectedFact!), true, null)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(completed.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Fact is null)
                    return Superseded("The registration was rejected.", null);
                return Result<ReceiverOutcome>.Success(step.Value.Applied
                    ? new ReceiverOutcome.Applied(step.Value.Fact)
                    : new ReceiverOutcome.Replayed(step.Value.Fact));
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The record changed concurrently; rehydrate and decide again.
            }
        }
    }

    public async ValueTask<Result<ReceiverOutcome>> RejectAsync(IExecutionContext context,
        RejectFirmStaffRegistration command, CancellationToken ct)
    {
        var recordStream = new FirmStaffRecord(command.OperationId).Stream;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.Target != recordStream ||
            command.Generation != FirmStaffEffects.Generation)
            return Permanent("The rejection names another staff stream or generation.");
        var record = await reader.HydrateAsync(new FirmStaffRecord(command.OperationId, command.CommandId), ct)
            .ConfigureAwait(false);
        if (record.Accepted is null || record.RegistrationIntent != command.FailureProof)
            return Permanent("A rejection requires the exact frozen registration of its staff record.");
        FirmStaffRegistrationIntent intent;
        try
        {
            intent = await FirmStaffContent.ReadIntentAsync(content, command.FailureProof, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Permanent($"The frozen registration cannot be verified: {exception.Message}");
        }

        var provenance = FirmStaffEffects.Child(command.CommandId, nameof(RejectFirmStaffRegistration),
            record.Accepted.Metadata.EventId, intent, recordStream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        if (record.Acceptance.Decide(binding) == NativeAcceptanceDecision.Replayed)
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(
                Fact(recordStream, record.Acceptance.SelectedFact!)));
        if (record.Acceptance.Decide(binding) == NativeAcceptanceDecision.Conflict)
            return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        if (record.IsRegistered)
            return Superseded("The registration already completed and recovers forward.", null);
        if (record.IsRejected)
            return Superseded("The registration was already rejected.", null);

        var claim = await reader.HydrateAsync(new FirmStaffIdentityClaim(intent.CanonicalUserId), ct)
            .ConfigureAwait(false);
        if (claim.ClaimedByStaffMemberId is not { } owner || owner == command.OperationId)
            return Permanent("Only a canonical user claimed by a different staff record rejects a registration.");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new FirmStaffRecord(command.OperationId, command.CommandId),
                    pending =>
                    {
                        switch (pending.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(Fact(pending.Stream, pending.Acceptance.SelectedFact!), false, null)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (!pending.IsPending)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(null, false, null)));
                        var rejected = pending.RejectRegistration(provenance, command.FailureProof);
                        return rejected.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(
                                new Step(Fact(pending.Stream, pending.Acceptance.SelectedFact!), true, null)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(rejected.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Fact is null)
                    return Superseded("The registration is no longer pending.", null);
                return Result<ReceiverOutcome>.Success(step.Value.Applied
                    ? new ReceiverOutcome.Applied(step.Value.Fact)
                    : new ReceiverOutcome.Replayed(step.Value.Fact));
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The record changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>
    ///     A claim owned by a different staff record rejects the pending registration with the same deterministic
    ///     command the process abort would use, then ends the process: nothing remains to claim or complete.
    /// </summary>
    async ValueTask<Result<ReceiverOutcome>> RejectForeignClaimAsync(IExecutionContext context,
        ClaimFirmStaffIdentity command, FirmStaffRegistrationIntent intent, CancellationToken ct)
    {
        var rejection = new RejectFirmStaffRegistration(
            FirmStaffEffects.ReceiverCommandId(command.Source.EventId, nameof(RejectFirmStaffRegistration), null,
                intent.Header.Episode), command.OperationId, new FirmStaffRecord(command.StaffMemberId).Stream,
            FirmStaffEffects.Generation, 0, command.Intent);
        var rejected = await RejectAsync(context, rejection, ct).ConfigureAwait(false);
        if (!rejected.IsSuccess)
            return rejected;
        var evidence = rejected.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            _ => null,
        };
        return Superseded("The canonical user is permanently claimed by another staff record.", evidence);
    }

    async ValueTask<Verified> VerifyAsync(Uuid staffMemberId, NativeFactReference source,
        ImmutableContentRef intentReference, Uuid userId, Uuid commandId, CancellationToken ct)
    {
        var record = await reader.HydrateAsync(new FirmStaffRecord(staffMemberId, commandId), ct).ConfigureAwait(false);
        if (record.Accepted is not { } accepted)
            return new Verified(null, null, Transient("The accepted registration is not visible yet."));
        if (source.Stream != record.Stream || accepted.Metadata.EventId != source.EventId ||
            accepted.Metadata.AggregateVersion != source.AggregateVersion || accepted.Intent != intentReference)
            return new Verified(null, null, Permanent("The source is not the accepted registration of this staff record."));
        FirmStaffRegistrationIntent intent;
        try
        {
            intent = await FirmStaffContent.ReadIntentAsync(content, intentReference, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return new Verified(null, null, Permanent($"The frozen registration cannot be verified: {exception.Message}"));
        }

        return intent.StaffMemberId != staffMemberId || intent.CanonicalUserId != userId ||
               intent.Header.RootOperationId != staffMemberId
            ? new Verified(null, null, Permanent("The command differs from the frozen registration intent."))
            : new Verified(record, intent, null);
    }

    static NativeFactReference Fact(EventStreamAddress stream, DomainEvent fact) => NativeFactReference.Of(stream, fact);

    static Result<ReceiverOutcome> Superseded(string reason, NativeFactReference? evidence) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Superseded(reason, evidence));

    static Result<ReceiverOutcome> Permanent(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, false));

    static Result<ReceiverOutcome> Transient(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, true));

    sealed record Verified(FirmStaffRecord? Record, FirmStaffRegistrationIntent? Intent,
        Result<ReceiverOutcome>? Failure);

    sealed record Step(NativeFactReference? Fact, bool Applied, FirmStaffIdentityClaimed? Foreign);
}
