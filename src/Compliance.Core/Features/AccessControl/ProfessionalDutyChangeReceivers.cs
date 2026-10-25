using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The assertion-claim, apply and reject receivers of <c>ProfessionalDutyChangeProcess</c>. Each verifies the
///     accepted change from the authoritative designation owner, resolves an exact replay from the target's accepted
///     facts, and appends one fact. For recording, claim and apply require the exact granted staff use, resolved from
///     the staff record by its reservation key. A transient wait is a transient conflict; a changed intent or invalid
///     proof is a permanent conflict.
/// </summary>
sealed class ProfessionalDutyChangeReceivers(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, ProfessionalDutyChangeVerifier verifier,
    ProfessionalDutyFences fences)
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> ClaimAsync(IExecutionContext context,
        ClaimProfessionalAssertionIdentity command, CancellationToken ct)
    {
        var claimStream = new ProfessionalAssertionIdentityClaim(command.AssertionId).Stream;
        if (command.CommandId == Uuid.Empty || command.AssertionId == Uuid.Empty || command.Target != claimStream ||
            command.Generation != ProfessionalDutyEffects.Generation || command.OperationId != command.AssertionId)
            return SourceUseReceiverSupport.Permanent("The claim names another assertion stream, generation or operation.");
        var change = await verifier.VerifyAsync(command.Source, command.Intent, command.AssertionId, command.CommandId,
            ct).ConfigureAwait(false);
        if (change.Error is not null)
            return Result<ReceiverOutcome>.Failure(change.Error);
        var (intent, evidence, owner) = (change.Intent!, change.Evidence!, change.Owner!);
        if (intent.DesignationId != command.DesignationId || evidence.EvidenceDigest != command.AssertionSha256)
            return SourceUseReceiverSupport.Permanent("The claim differs from the frozen assertion tuple.");
        if (owner.IsRejected(command.AssertionId))
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected("The change was rejected.", null));
        var held = await RequireStaffUseAsync(change, command.StaffEligibilityUse, ct).ConfigureAwait(false);
        if (held.Failure is not null)
            return held.Failure.Value;

        var provenance = ProfessionalDutyEffects.Child(command.CommandId, nameof(ClaimProfessionalAssertionIdentity),
            command.Source.EventId, intent, claimStream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(
                    new ProfessionalAssertionIdentityClaim(command.AssertionId, command.CommandId), claim =>
                    {
                        switch (claim.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(Fact(claim.Stream, claim.Acceptance.SelectedFact!), false, false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (claim.Claimed is { } existing)
                            return AggregateOutcome.Discard(Result<Step>.Success(existing.Intent == command.Intent
                                ? new Step(Fact(claim.Stream, existing), false, false)
                                : new Step(null, false, true)));
                        var claimed = claim.Claim(provenance, command.Intent);
                        return claimed.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(
                                new Step(Fact(claim.Stream, claim.Acceptance.SelectedFact!), true, false)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(claimed.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Foreign)
                    return await fences.RejectAndFenceAsync(change, command.Source, command.Intent,
                        "The signed assertion identity is permanently claimed by a different change.", context, ct)
                        .ConfigureAwait(false);
                return step.Value.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact!)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent claimant won the append; rehydrate and decide again.
            }
        }
    }

    public async ValueTask<Result<ReceiverOutcome>> ApplyAsync(IExecutionContext context,
        ApplyProfessionalDutyChange command, CancellationToken ct)
    {
        if (command.CommandId == Uuid.Empty || command.AssertionId == Uuid.Empty ||
            command.Generation != ProfessionalDutyEffects.Generation || command.OperationId != command.AssertionId)
            return SourceUseReceiverSupport.Permanent("The apply names another generation or operation.");
        var change = await verifier.VerifyAsync(command.Source, command.Intent, command.AssertionId, command.CommandId,
            ct).ConfigureAwait(false);
        if (change.Error is not null)
            return Result<ReceiverOutcome>.Failure(change.Error);
        var (intent, evidence, owner) = (change.Intent!, change.Evidence!, change.Owner!);
        if (command.Target != owner.Stream || intent.DesignationId != command.DesignationId)
            return SourceUseReceiverSupport.Permanent("The apply names another designation stream or designation.");
        if (owner.IsRejected(command.AssertionId))
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected("The change was rejected.", null));

        var claim = await reader.HydrateAsync(new ProfessionalAssertionIdentityClaim(command.AssertionId), ct)
            .ConfigureAwait(false);
        if (!claim.IsClaimed)
            return SourceUseReceiverSupport.Transient("The permanent assertion claim is not visible yet.");
        if (claim.Claimed!.Intent != command.Intent)
            return SourceUseReceiverSupport.Permanent("The assertion is claimed by a different change.");
        var held = await RequireStaffUseAsync(change, command.StaffEligibilityUse, ct).ConfigureAwait(false);
        if (held.Failure is not null)
            return held.Failure.Value;

        var provenance = ProfessionalDutyEffects.Child(command.CommandId, nameof(ApplyProfessionalDutyChange),
            command.Source.EventId, intent, owner.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new FirmProfessionalDutyDesignation(owner.UserId, owner.Duty,
                    owner.TenantId, command.CommandId), ct).ConfigureAwait(false);
                if (current.Acceptance.Decide(binding) is var decision and not NativeAcceptanceDecision.None)
                    return decision == NativeAcceptanceDecision.Replayed
                        ? SourceUseReceiverSupport.Replayed(Fact(current.Stream, current.Acceptance.SelectedFact!))
                        : Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
                if (current.IsRejected(command.AssertionId))
                    return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected("The change was rejected.", null));

                var applied = await BuildAsync(current, intent, evidence, provenance, command.CommandId, ct)
                    .ConfigureAwait(false);
                if (applied.Error is not null)
                    return Result<ReceiverOutcome>.Failure(applied.Error);
                var latestEvent = EventIdOf(current.Latest);
                var step = await executor.ExecuteAsync(new FirmProfessionalDutyDesignation(owner.UserId, owner.Duty,
                    owner.TenantId, command.CommandId), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(Fact(fresh.Stream, fresh.Acceptance.SelectedFact!), false, false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.IsRejected(command.AssertionId))
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(null, false, true)));
                        if (EventIdOf(fresh.Latest) != latestEvent || fresh.Revision != current.Revision)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(null, false, false)));
                        var raised = fresh.Apply(intent.ChangeKind, provenance, applied.Designation!,
                            intent.SignedAssertion, applied.Result!);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(
                                new Step(Fact(fresh.Stream, fresh.Acceptance.SelectedFact!), true, false)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Foreign)
                    return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected("The change was rejected.", null));
                if (step.Value.Fact is null)
                {
                    if (attempt >= MaximumAttempts)
                        return SourceUseReceiverSupport.Transient("The designation kept changing while the change was applied.");
                    continue;
                }

                return step.Value.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The designation changed concurrently; rehydrate and decide again.
            }
        }
    }

    public async ValueTask<Result<ReceiverOutcome>> RejectAsync(IExecutionContext context,
        RejectProfessionalDutyChange command, CancellationToken ct)
    {
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != ProfessionalDutyEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The rejection names another generation or operation.");
        ProfessionalDutyChangeIntent intent;
        VerifiedFirmProfessionalDutyAssertion evidence;
        try
        {
            intent = await ProfessionalDutyContent.ReadIntentAsync(content, command.FailureProof, ct)
                .ConfigureAwait(false);
            evidence = await ProfessionalDutyContent.ReadVerifiedScopeAsync(content, intent.VerifiedScope, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen change cannot be verified: {exception.Message}");
        }

        var owner = new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty, evidence.TenantId,
            command.CommandId);
        if (intent.Header.RootOperationId != command.OperationId || command.Target != owner.Stream)
            return SourceUseReceiverSupport.Permanent("The rejection names another designation stream or operation.");
        var hydrated = await reader.HydrateAsync(owner, ct).ConfigureAwait(false);
        if (hydrated.AcceptedFor(command.OperationId) is not { } accepted || accepted.Intent != command.FailureProof)
            return SourceUseReceiverSupport.Permanent("A rejection requires the exact frozen change of its designation.");

        var provenance = ProfessionalDutyEffects.Child(command.CommandId, nameof(RejectProfessionalDutyChange),
            accepted.Metadata.EventId, intent, owner.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new FirmProfessionalDutyDesignation(evidence.UserId,
                    evidence.Duty, evidence.TenantId, command.CommandId), pending =>
                    {
                        switch (pending.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(Fact(pending.Stream, pending.Acceptance.SelectedFact!), false, false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (pending.TerminalFor(command.OperationId) is { } terminal)
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                new Step(terminal is ProfessionalDutyChangeRejected
                                    ? Fact(pending.Stream, terminal)
                                    : null, false, true)));
                        var rejected = pending.Reject(provenance, command.FailureProof);
                        return rejected.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(
                                new Step(Fact(pending.Stream, pending.Acceptance.SelectedFact!), true, false)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(rejected.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Foreign)
                    return SourceUseReceiverSupport.Superseded(step.Value.Fact is null
                        ? "The change already applied and recovers forward."
                        : "The change was already rejected.", step.Value.Fact);
                return step.Value.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact!)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The designation changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>For recording, the exact granted and still active staff use; none for a revocation.</summary>
    async ValueTask<Held> RequireStaffUseAsync(ProfessionalDutyChange change, NativeFactReference? named,
        CancellationToken ct)
    {
        var requirement = change.Intent!.StaffEligibility;
        if (requirement is null)
            return new Held(null);
        var assertionId = change.Intent.VerifiedAssertionId;
        var staff = await reader.HydrateAsync(new FirmStaffRecord(
            Uuid.Parse(requirement.SourceOwner.Resource, System.Globalization.CultureInfo.InvariantCulture)).ForSourceUse(candidate => ProfessionalDutyRules.StaffUseKey(assertionId, change.Owner!.Stream, requirement)), ct)
            .ConfigureAwait(false);
        var key = ProfessionalDutyRules.StaffUseKey(assertionId, change.Owner!.Stream, requirement);
        if (staff.Uses.Find(key) is not { } entry)
            return new Held(SourceUseReceiverSupport.Transient("The staff use is not visible yet."));
        if (entry.IsFence)
            return new Held(SourceUseReceiverSupport.Superseded("The staff use was fenced before it was granted.",
                NativeFactReference.Of(staff.Stream, entry.SettleFact!)));
        var grant = staff.Uses.GrantOf(key)!;
        if (named is not null && named != grant.GrantEvent)
            return new Held(SourceUseReceiverSupport.Permanent("The named staff use is not the granted use."));
        return entry.IsSettled && !change.Owner.IsApplied(assertionId)
            ? new Held(SourceUseReceiverSupport.Permanent("The staff use ended before the change was applied."))
            : new Held(null);
    }

    async ValueTask<Built> BuildAsync(FirmProfessionalDutyDesignation current, ProfessionalDutyChangeIntent intent,
        VerifiedFirmProfessionalDutyAssertion evidence, OperationProvenance provenance, Uuid commandId,
        CancellationToken ct)
    {
        if (!ProfessionalDutyRules.ValidAt(evidence, intent.Header.AcceptedAt))
            return Built.Failed(Permanent("The signed assertion was not valid at the declared decision instant."));
        FirmProfessionalDutyDesignationView? previous;
        try
        {
            previous = current.LatestContent is { } latest
                ? await ProfessionalDutyContent.ReadDesignationAsync(content, latest, ct).ConfigureAwait(false)
                : null;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Built.Failed(Permanent($"The current designation cannot be verified: {exception.Message}"));
        }

        var chain = ProfessionalDutyRules.CheckChain(intent.ChangeKind, evidence, previous);
        if (!chain.IsSuccess)
            return Built.Failed(Permanent(chain.Error!.Message));
        var revision = (long)current.Revision + 1;
        FirmProfessionalDutyDesignationView view;
        if (intent.ChangeKind == ProfessionalDutyChangeKind.Record)
        {
            var requirement = intent.StaffEligibility!;
            var staffId = Uuid.Parse(requirement.SourceOwner.Resource, System.Globalization.CultureInfo.InvariantCulture);
            if (previous is not null && previous.DesignationId == evidence.DesignationId &&
                previous.StaffMemberId != staffId)
                return Built.Failed(Permanent("The signed assertion names another staff member than the retained designation."));
            view = ProfessionalDutyRules.Recorded(previous, evidence, staffId, (long)requirement.ResourceRevision,
                revision, provenance.Actor, intent.Header.AcceptedAt);
        }
        else
        {
            view = ProfessionalDutyRules.Revoked(previous!, evidence, revision, provenance.Actor,
                intent.Header.AcceptedAt);
        }

        var tag = $"{revision}:{EventIdOf(current.Latest)}";
        var designation = await ProfessionalDutyContent.WriteDesignationAsync(content,
            Uuid.CreateVersion5(commandId, $"designation-content:{tag}"), intent.ChangeKind, view, ct)
            .ConfigureAwait(false);
        var result = await ProfessionalDutyContent.WriteResultAsync(content,
            Uuid.CreateVersion5(commandId, $"operation-result:{tag}"), intent.ChangeKind, designation, ct)
            .ConfigureAwait(false);
        return new Built(designation, result, null);

        static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);
    }

    static Uuid? EventIdOf(INativeOperationFact? fact) => (fact as DomainEvent)?.Metadata?.EventId;

    static NativeFactReference Fact(EventStreamAddress stream, DomainEvent fact) => NativeFactReference.Of(stream, fact);

    sealed record Held(Result<ReceiverOutcome>? Failure);

    sealed record Built(ImmutableContentRef? Designation, ImmutableContentRef? Result, RequestError? Error)
    {
        public static Built Failed(RequestError error) => new(null, null, error);
    }

    /// <summary>Fact is the appended or replayed fact; Foreign marks the superseded or foreign-claim branch.</summary>
    sealed record Step(NativeFactReference? Fact, bool Applied, bool Foreign);
}
