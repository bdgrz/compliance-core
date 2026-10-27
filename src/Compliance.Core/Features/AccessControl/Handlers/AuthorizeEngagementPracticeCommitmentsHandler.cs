using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Authorizes an admitted acceptance or staff assignment irreversibly. Every temporary reservation and every
///     source use is re-proven from its authoritative owner (the exact grant is active and is the one the acquire
///     stage produced), every temporal validity interval and every external source is judged at the server-frozen
///     decision instant, and only then is the fact appended. An operation that was rejected stays fenced; a source
///     that does not hold rejects the whole process so the abort path fences it and settles every grant.
/// </summary>
sealed class AuthorizeEngagementPracticeCommitmentsHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementAcceptanceVerifier verifier,
    IPlatformUserDirectoryReader platformUsers, BoundaryReader boundarySources)
    : IRequestHandler<AuthorizeEngagementPracticeCommitments, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<AuthorizeEngagementPracticeCommitments> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != EngagementAcceptanceEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The authorization names another operation or generation.");
        var loaded = await verifier.LoadAsync(command.Source, command.Intent, command.OperationId, Uuid.Empty,
            command.CommandId, ct).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (owner, operation, intent) = (loaded.Owner!, loaded.Operation!, loaded.Intent!);
        if (command.Target != owner.Stream)
            return SourceUseReceiverSupport.Permanent("The authorization names another engagement stream.");

        var now = clock.GetUtcNow();
        var provenance = EngagementAcceptanceEffects.Child(command.CommandId,
            nameof(AuthorizeEngagementPracticeCommitments), command.Source.EventId, intent, owner.Stream, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream,
                    owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        if (operation.Rejected is not null)
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(
                "The operation was rejected; authorization is fenced.",
                NativeFactReference.Of(owner.Stream, operation.Rejected)));
        if (operation.Authorized is not null)
            return SourceUseReceiverSupport.Superseded("The operation is already authorized.",
                NativeFactReference.Of(owner.Stream, operation.Authorized));

        var tenantId = EngagementIndependenceIntent.TenantOf(intent.Header);
        var held = await VerifyHoldsAsync(loaded, command, tenantId, ct).ConfigureAwait(false);
        if (held is not null)
            return Result<ReceiverOutcome>.Failure(held);
        try
        {
            if (await ValidAtAsync(loaded, tenantId, now, ct).ConfigureAwait(false) is { } invalid)
                return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(invalid, null));
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Result<ReceiverOutcome>.Failure(EngagementAcceptanceVerifier.Transient(
                $"The pinned source content is not visible yet: {exception.Message}"));
        }

        var proofs = await content.WriteJsonAsync(IndependenceContent.Realm(tenantId),
            Uuid.CreateVersion5(command.Source.EventId, "engagement-independence-authorization-proofs"),
            EngagementIndependenceIntent.ProofSchema,
            new EngagementAuthorizationProofs(command.ReservationProofManifest, command.SourceUseProofManifest), ct)
            .ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new ServiceEngagementIndependence(owner.TenantId,
                    owner.EngagementId, command.CommandId, command.OperationId), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), false,
                                        null)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.ReplayCut.Digest != owner.ReplayCut.Digest)
                            return AggregateOutcome.Discard(Result<Step>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The engagement native prefix changed during receiver preparation; retry from its exact current cut.", true)));

                        if (fresh.OperationOf(command.OperationId) is { Rejected: { } rejected })
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, rejected), false,
                                "The operation was rejected; authorization is fenced.")));
                        var authorized = fresh.AuthorizeOperation(provenance, now, proofs);
                        return authorized.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), true, null)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(authorized.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Fenced is { } fenced)
                    return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(fenced, step.Value.Fact));
                return step.Value.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent delivery or rejection won the append; rehydrate and decide again.
            }
        }
    }

    /// <summary>
    ///     Every required use and every practice subject is held by its exact grant, resolved from the owner and not
    ///     from the proof alone; the proofs cover exactly the frozen manifests.
    /// </summary>
    async ValueTask<RequestError?> VerifyHoldsAsync(LoadedOperation loaded, AuthorizeEngagementPracticeCommitments command,
        Uuid tenantId, CancellationToken ct)
    {
        var (engagement, intent) = (loaded.Owner!.Stream, loaded.Intent!);
        var realm = IndependenceContent.Realm(tenantId);
        NativeFactReference[] uses;
        NativeFactReference[] reservations;
        try
        {
            uses = await content.ReadJsonAsync<NativeFactReference[]>(realm, command.SourceUseProofManifest, ct)
                .ConfigureAwait(false);
            reservations = await content.ReadJsonAsync<NativeFactReference[]>(realm, command.ReservationProofManifest,
                ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return exception is ImmutableContentUnavailableException
                ? EngagementAcceptanceVerifier.Transient($"The grant proofs are not visible yet: {exception.Message}")
                : EngagementAcceptanceVerifier.Permanent($"The grant proofs cannot be verified: {exception.Message}");
        }

        if (uses.Length != loaded.Requirements!.Count || reservations.Length != loaded.Subjects!.Count)
            return EngagementAcceptanceVerifier.Permanent(
                "The proofs do not cover exactly the frozen source uses and practice subjects.");
        foreach (var requirement in loaded.Requirements)
        {
            var key = EngagementAcceptanceEffects.UseKey(command.OperationId, engagement, requirement.SourceOwner);
            var source = await EngagementUseOwners.HydrateAsync(reader, requirement.SourceOwner, intent.Header, tenantId, key, ct)
                .ConfigureAwait(false);
            if (source is null || source.Stream != requirement.SourceOwner)
                return EngagementAcceptanceVerifier.Permanent("A required source use names an owner that cannot be resolved.");
            if (source.Uses.Find(key) is not { IsActive: true } entry)
                return EngagementAcceptanceVerifier.Transient(
                    "A required source use is not held yet; authorization waits for the complete set.");
            if (!uses.Contains(NativeFactReference.Of(source.Stream, entry.GrantFact!)))
                return EngagementAcceptanceVerifier.Permanent(
                    "The proofs do not name the exact grant that holds a required source use.");
        }

        foreach (var subject in loaded.Subjects)
        {
            var held = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId, subject.Kind,
                subject.SubjectId).ForSourceUse(candidate => EngagementAcceptanceEffects.UseKey(command.OperationId, engagement, candidate.Stream)), ct).ConfigureAwait(false);
            var key = EngagementAcceptanceEffects.UseKey(command.OperationId, engagement, held.Stream);
            if (held.Uses.Find(key) is not { IsActive: true } entry)
                return EngagementAcceptanceVerifier.Transient(
                    "A practice reservation is not held yet; authorization waits for the complete set.");
            if (held.Practice is not null && held.Practice != subject.Practice)
                return EngagementAcceptanceVerifier.Permanent(
                    "A subject permanently serves the opposite practice for this client.");
            if (!reservations.Contains(NativeFactReference.Of(held.Stream, entry.GrantFact!)))
                return EngagementAcceptanceVerifier.Permanent(
                    "The proofs do not name the exact grant that reserves a practice subject.");
        }

        return null;
    }

    /// <summary>
    ///     Judges every temporal validity interval and every external source at the frozen decision instant. A pinned
    ///     source cannot change while held, so this only judges what a hold cannot freeze: validity intervals, the
    ///     existence of the people involved and the approval of a selected boundary.
    /// </summary>
    async ValueTask<string?> ValidAtAsync(LoadedOperation loaded, Uuid tenantId, DateTimeOffset decisionAt,
        CancellationToken ct)
    {
        try
        {
            foreach (var requirement in loaded.Requirements!)
            {
                switch (requirement.SourceOwner.Area)
                {
                    case FirmProfessionalDutyDesignation.Area:
                        var duty = await ProfessionalDutyContent.ReadDesignationAsync(content,
                            requirement.RequiredFacts, ct).ConfigureAwait(false);
                        if (duty.DesignatedAt > decisionAt || duty.NotBefore > decisionAt || duty.ExpiresAt <= decisionAt)
                            return "The partner designation is not valid at the decision instant.";
                        break;
                    case FirmStaffRecord.Area:
                        var staff = await FirmStaffContent.ReadStaffAsync(content, requirement.RequiredFacts, ct)
                            .ConfigureAwait(false);
                        if (!staff.IsActive || staff.RecordedAt > decisionAt)
                            return "A staff record is not active or is dated after the decision instant.";
                        if (!await platformUsers.ExistsAsync(staff.UserId, ct).ConfigureAwait(false))
                            return "A staff member no longer has a platform user identity.";
                        break;
                    case IndependenceRuleCatalog.Area:
                        var rules = await content.ReadJsonAsync<IndependenceRuleVersionView>(
                            EngagementPersonalDecisionEffects.PlatformRealm, requirement.RequiredFacts, ct)
                            .ConfigureAwait(false);
                        if (rules.RecordedAt > decisionAt || rules.Ratification?.RecordedAt > decisionAt)
                            return "The ratified rules are dated after the decision instant.";
                        break;
                }
            }

            var evidence = await content.ReadJsonAsync<EngagementAcceptanceEvidence>(IndependenceContent.Realm(tenantId),
                loaded.Intent!.Evidence, ct).ConfigureAwait(false);
            if (evidence.Proof?.Boundary is { } boundary)
            {
                var source = await boundarySources.ReadAsync(tenantId, boundary.BoundaryId, ct).ConfigureAwait(false);
                if (!source.IsCreated || !source.IsVisible || !source.IsVersionApproved(boundary.VersionId))
                    return "The selected boundary version is no longer a currently retained approved version.";
            }
        }
        catch (ImmutableContentIntegrityException exception) when (exception is not ImmutableContentUnavailableException)
        {
            return $"The pinned source content cannot be verified: {exception.Message}";
        }

        return null;
    }

    sealed record Step(NativeFactReference Fact, bool Applied, string? Fenced);
}
