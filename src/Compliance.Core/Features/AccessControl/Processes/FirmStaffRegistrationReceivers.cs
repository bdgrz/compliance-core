using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
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
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Processes;

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
            command.Source is null || command.Intent is null ||
            command.Target != claimStream || command.Generation != FirmStaffEffects.Generation ||
            command.OperationId != command.StaffMemberId || command.ExpectedRevision is not (null or 0))
            return Permanent("The claim names another canonical user stream, generation or operation.");
        var verified = await VerifyAsync(command.StaffMemberId, command.Source, command.Intent, command.UserId,
            command.CommandId, ct).ConfigureAwait(false);
        if (verified.Failure is not null)
            return verified.Failure.Value;
        if (command.Practice != verified.Intent!.Practice)
            return Permanent("The claim practice differs from the frozen registration.");
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

                        if (claim.ClaimedByStaffMemberId == command.StaffMemberId && claim.Claimed?.Intent != command.Intent)
                            return AggregateOutcome.Discard(Result<Step>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The permanent claim differs from the exact registered immutable identity.")));
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
            command.Source is null || command.Intent is null ||
            command.Target != recordStream || command.Generation != FirmStaffEffects.Generation ||
            command.OperationId != command.StaffMemberId || command.ExpectedRevision is not (null or 0))
            return Permanent("The completion names another staff stream, generation or operation.");
        var verified = await VerifyAsync(command.StaffMemberId, command.Source, command.Intent, command.UserId,
            command.CommandId, ct).ConfigureAwait(false);
        if (verified.Failure is not null)
            return verified.Failure.Value;
        if (verified.Record!.IsRejected)
            return Superseded("The registration was rejected.", null);

        var intent = verified.Intent!;
        var provenance = FirmStaffEffects.Child(command.CommandId, nameof(CompleteFirmStaffRegistration),
            command.Source.EventId, intent, recordStream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        Result<ReceiverOutcome>? Decide(FirmStaffRecord owner) => owner.Acceptance.Decide(binding) switch
        {
            NativeAcceptanceDecision.Replayed => Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(
                Fact(owner.Stream, owner.Acceptance.SelectedFact!))),
            NativeAcceptanceDecision.Conflict => Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError),
            _ when owner.IsRejected => Superseded("The registration was rejected.", null),
            _ => null,
        };
        var prepared = await NativeReceiverPreparation.PrepareAsync<FirmStaffRecord, Completion>(reader,
            () => new FirmStaffRecord(command.StaffMemberId, command.CommandId), Decide,
            async (owner, token) =>
            {
                var claim = await reader.HydrateAsync(new FirmStaffIdentityClaim(command.UserId), token)
                    .ConfigureAwait(false);
                if (!claim.IsClaimed)
                    return Result<Completion>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The permanent canonical-user claim is not visible yet.", true));
                if (claim.ClaimedByStaffMemberId != command.StaffMemberId || claim.Claimed!.Intent != command.Intent)
                    return Result<Completion>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The canonical user is claimed by another staff registration."));
                var staff = await FirmStaffContent.WriteStaffAsync(content,
                    Uuid.CreateVersion5(command.CommandId, "staff-content"), FirmStaffContent.RegisteredView(intent), token)
                    .ConfigureAwait(false);
                var result = await FirmStaffContent.WriteResultAsync(content,
                    Uuid.CreateVersion5(command.CommandId, "operation-result"), staff, token).ConfigureAwait(false);
                return Result<Completion>.Success(new Completion(staff, result, owner.Version,
                    owner.CommittedStreamPosition, owner.ReplayCut.Digest));
            }, ct).ConfigureAwait(false);
        if (!prepared.IsSuccess)
            return Result<ReceiverOutcome>.Failure(prepared.Error!);
        if (prepared.Value!.Outcome is { } known)
            return known;
        var candidate = prepared.Value.Value!;
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
                        if ((record.Version, record.CommittedStreamPosition, record.ReplayCut.Digest) !=
                            (candidate.Version, candidate.Position, candidate.Digest))
                            return AggregateOutcome.Discard(Result<Step>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The prepared staff completion prefix changed before append.", true)));
                        var completed = record.CompleteRegistration(provenance, candidate.Staff, candidate.Result);
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
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.FailureProof is null ||
            command.Target != recordStream ||
            command.Generation != FirmStaffEffects.Generation || command.ExpectedRevision != 0)
            return Permanent("The rejection names another staff stream or generation.");
        var record = await reader.HydrateAsync(new FirmStaffRecord(command.OperationId, command.CommandId), ct)
            .ConfigureAwait(false);
        if (record.Accepted is null || record.RegistrationIntent != command.FailureProof)
            return Permanent("A rejection requires the exact frozen registration of its staff record.");
        FirmStaffRegistrationIntent intent;
        try
        {
            intent = await FirmStaffContent.ReadIntentAsync(content, command.FailureProof, ct).ConfigureAwait(false);
            FirmStaffContent.RequireIntent(record, intent);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return exception is ImmutableContentUnavailableException
                ? Transient($"The frozen registration is not available: {exception.Message}")
                : Permanent($"The frozen registration cannot be verified: {exception.Message}");
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
            FirmStaffContent.RequireIntent(record, intent);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return new Verified(null, null, exception is ImmutableContentUnavailableException
                ? Transient($"The frozen registration is not available: {exception.Message}")
                : Permanent($"The frozen registration cannot be verified: {exception.Message}"));
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

    sealed record Completion(ImmutableContentRef Staff, ImmutableContentRef Result, ulong Version, ulong Position,
        string Digest);

    sealed record Verified(FirmStaffRecord? Record, FirmStaffRegistrationIntent? Intent,
        Result<ReceiverOutcome>? Failure);

    sealed record Step(NativeFactReference? Fact, bool Applied, FirmStaffIdentityClaimed? Foreign);
}
