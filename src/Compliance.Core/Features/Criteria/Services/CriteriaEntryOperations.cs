using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Admits one entry operation against its exact source owner.</summary>
/// <remarks>
/// Structural proposals remain pending until their separate source-ordered
/// decision is recorded. This service has no default fresh-head provider and
/// must not be enabled as a complete participant protocol by itself.
/// </remarks>
public sealed class CriteriaEntryOperations(IAggregateReader aggregates, IAggregateExecutor executor,
    ICriteriaExactSourceReader source, ICriteriaOperationReader receipts,
    IAccessAdmissionAuthority? admission = null, ICriteriaFreshEntryHead? freshHead = null,
    ICriteriaSourceCutCoverage? sourceCoverage = null, ICriteriaPublicationAuthority? publications = null,
    ICriteriaParticipantCausalOrder? causal = null, ICriteriaAuthoringReadiness? readiness = null)
{
    /// <summary>Records one structural proposal; a participant still decides it.</summary>
    public ValueTask<Result<CriteriaOperationReceipt>> ProposeAsync(IRequestContext<ProposeCriterionChange> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var intent = new CriterionChangeIntent(request.EditionId, request.DraftGeneration, request.Identifier,
            request.OperationId, request.ExpectedRevision, request.Action, request.Kind, request.Category,
            request.ParentIdentifier, request.ParentIncarnationId, request.Summary);
        return ExecuteAsync(context, intent.EditionId, intent.Identifier, intent.OperationId, intent, ct);
    }

    /// <summary>Records a product-summary correction where its exact operation can be proved.</summary>
    public ValueTask<Result<CriteriaOperationReceipt>> CorrectSummaryAsync(IRequestContext<CorrectCriterionSummary> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var intent = new CriterionSummaryIntent(request.EditionId, request.Identifier, request.OperationId,
            request.ExpectedRevision, request.Summary);
        return ExecuteAsync(context, intent.EditionId, intent.Identifier, intent.OperationId, intent, ct);
    }

    async ValueTask<Result<CriteriaOperationReceipt>> ExecuteAsync<TRequest, TIntent>(IRequestContext<TRequest> context,
        Uuid editionId, string identifier, Uuid operationId, TIntent intent, CancellationToken ct)
        where TRequest : IRequestBase where TIntent : notnull
    {
        if (editionId == Uuid.Empty || operationId == Uuid.Empty || !CriteriaContentBounds.Identifier(identifier) ||
            !string.Equals(identifier, identifier.Trim(), StringComparison.Ordinal))
            return Invalid();
        if (intent is CriterionChangeIntent boundedStructural && !CriteriaContentBounds.StructuralShape(boundedStructural) ||
            intent is CriterionSummaryIntent boundedSummary &&
            (!CriteriaContentBounds.SummaryIntent(boundedSummary) || boundedSummary.ExpectedRevision < 0))
            return Invalid();
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Forbidden();
        if (admission is null || freshHead is null ||
            !await CriteriaAuthoringReadiness.CheckAsync(readiness, ct).ConfigureAwait(false))
            return Unavailable();

        var identity = context.Actor.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated &&
            string.Equals(candidate.FindFirst("iss")?.Value, "bdgrz", StringComparison.Ordinal));
        var subject = identity?.FindFirst("sub")?.Value;
        var issuer = identity?.FindFirst("iss")?.Value;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer))
            return Forbidden();
        var actor = new ActorAttribution(subject, issuer);
        var selectedOwner = new CriteriaEntry(editionId, identifier, selectedOperationId: operationId);

        SelectedHistory selected;
        try
        {
            selected = await ReadSelectedAsync(selectedOwner, operationId, ct).ConfigureAwait(false);
        }
        catch (CriteriaSourceUnavailableException)
        {
            return Unavailable();
        }
        var observed = await aggregates.HydrateAsync(selectedOwner, ct).ConfigureAwait(false);
        var execution = selected.RecordedGeneration ?? observed.MinimumExecutionGeneration;
        var authoredDigest = intent switch
        {
            CriterionChangeIntent value => AuthoredIntentDigest.Compute(value),
            CriterionSummaryIntent value => AuthoredIntentDigest.Compute(value),
            _ => throw new InvalidOperationException("Unsupported criteria entry intent."),
        };
        var boundDigest = CriteriaEdition.BindAdmissionDigest(authoredDigest, execution);
        AccessAdmissionEvidence? evidence;
        try
        {
            evidence = await admission.AdmitCriteriaOperatorAsync(context, operationId,
                observed.Stream.ToString(), boundDigest, ct).ConfigureAwait(false);
        }
        catch (AccessAdmissionUnavailableException)
        {
            return Unavailable();
        }
        if (evidence is not null && !AccessAdmissionEvidenceBinding.IsCurrentFormat(evidence))
            return Unavailable();
        if (evidence is null || !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(evidence, userId, actor,
                operationId, observed.Stream.ToString(), boundDigest))
            return Forbidden();
        if (!CriteriaContentBounds.AdmissionFields(actor, evidence))
            return Conflict("The operator admission evidence exceeds the criteria event payload limit.");

        if (selected.SeedProposal is { } seedProposal)
        {
            try
            {
                if (!await new CriteriaSeedBindingVerifier(source).VerifyProposalAsync(seedProposal, selected.SeedArea, ct)
                        .ConfigureAwait(false))
                    return Pending(editionId, operationId);
            }
            catch (CriteriaSourceUnavailableException)
            {
                return Pending(editionId, operationId);
            }
            return Conflict("The operation identity is already reserved for a configured seed entry.");
        }
        if (selected.Intent is not null && (!Equals(selected.Intent, intent) || selected.Actor != actor))
            return Conflict("The operation identity is already bound to different intent.");
        if (selected.NextResourceOffset != observed.CommittedStreamPosition ||
            !await freshHead.IsCurrentAsync(observed.Stream, observed.Id, selected.NextResourceOffset, ct)
                .ConfigureAwait(false))
            return Unavailable();

        // Resolve an existing operation claim before returning any state-based
        // preflight refusal. A prior exact claim without a terminal outcome must
        // remain pending, while a changed intent must not be hidden by target state.
        var priorClaim = await InspectOperationClaimAsync(editionId, identifier, operationId, intent, actor,
            evidence.UserId, ct).ConfigureAwait(false);
        if (!priorClaim.IsSuccess)
            return priorClaim.Error!.IsTransient ? Pending(editionId, operationId) :
                Result<CriteriaOperationReceipt>.Failure(priorClaim.Error);

        Uuid? parentAcceptance = null;
        if (intent is CriterionChangeIntent structural)
        {
            if (structural.DraftGeneration == Uuid.Empty)
                return Invalid();
            try
            {
                if (!await DraftWasEstablishedAsync(structural.EditionId, structural.DraftGeneration, ct)
                        .ConfigureAwait(false))
                    return Pending(editionId, operationId);
            }
            catch (CriteriaSourceUnavailableException)
            {
                return Unavailable();
            }
        }
        if (intent is CriterionChangeIntent change && change.Kind == "point_of_focus" && change.Action != "remove")
        {
            if (change.ParentIdentifier is null || change.ParentIncarnationId is null || change.Category is null)
                return Invalid();
            try
            {
                parentAcceptance = await FindParentAcceptanceAsync(change, ct).ConfigureAwait(false);
            }
            catch (CriteriaSourceUnavailableException)
            {
                return Unavailable();
            }
            if (parentAcceptance is null)
                return Pending(editionId, operationId);
            if (selected.ParentAcceptedEventId is { } previous && previous != parentAcceptance)
                return Conflict("The operation identity is bound to different parent evidence.");
        }

        // Check deterministic target-state refusals against the already qualified
        // observation before creating a durable operation identity. This owner is
        // discarded; the real decision is made again at the captured position.
        var settleStaleSummary = intent is CriterionSummaryIntent summaryIntent && priorClaim.Value &&
            observed.Receipt(operationId)?.Status != "accepted" &&
            (summaryIntent.ExpectedRevision != observed.Revision || observed.Receipt(operationId)?.Status == "refused");
        var preflight = intent switch
        {
            CriterionChangeIntent preflightChange => observed.PreviewProposal(preflightChange, execution, actor, evidence, parentAcceptance),
            CriterionSummaryIntent when settleStaleSummary => Result.Success,
            CriterionSummaryIntent summary => observed.PreviewSummary(summary, execution, actor, evidence),
            _ => throw new InvalidOperationException("Unsupported criteria entry intent."),
        };
        if (!preflight.IsSuccess)
            // Even an observed absent claim does not exclude an in-flight
            // same-ID attempt that can claim immediately after the read.
            return Pending(editionId, operationId);

        // Structural and summary commands must first compete for one operation
        // identity. A no-change summary may not leave its ID available to a
        // different structural intent.
        var claim = await EnsureOperationClaimAsync(context, editionId, identifier, operationId, intent,
            execution, actor, evidence, ct).ConfigureAwait(false);
        if (!claim.IsSuccess)
            return Result<CriteriaOperationReceipt>.Failure(claim.Error!);
        if (claim.Value is not { } claimFact)
            return Pending(editionId, operationId);

        var producedFact = false;
        if (!await CriteriaAuthoringReadiness.CheckAsync(readiness, ct).ConfigureAwait(false))
            return Pending(editionId, operationId);
        try
        {
            var result = await executor.ExecuteAsync(new CriteriaEntry(editionId, identifier, operationId), owner =>
            {
                // Never silently rebase a previously admitted execution after a competing append.
                if (owner.CommittedStreamPosition != selected.NextResourceOffset)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The entry changed before the operation could be recorded.", true)));
                var before = owner.Version;
                var decision = intent switch
                {
                    CriterionChangeIntent change => owner.Propose(change, execution, actor, evidence, parentAcceptance, claimFact),
                    CriterionSummaryIntent summary when settleStaleSummary => owner.RefuseSummaryRevision(summary, execution, actor, evidence, claimFact),
                    CriterionSummaryIntent summary => owner.CorrectSummary(summary, execution, actor, evidence, claimFact),
                    _ => throw new InvalidOperationException("Unsupported criteria entry intent."),
                };
                producedFact = owner.Version != before;
                return AggregateOutcome.CommitOnSuccess(decision);
            }, context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                // The identity is now durable but no terminal outcome is. A
                // concurrent target change must not masquerade as a completed refusal.
                return Pending(editionId, operationId);
        }
        catch (EventStreamConcurrencyException)
        {
            return Pending(editionId, operationId);
        }
        catch (FitzException)
        {
            // The append may have committed even when its acknowledgement failed.
            return Pending(editionId, operationId);
        }
        catch (IOException)
        {
            return Pending(editionId, operationId);
        }
        catch (TimeoutException)
        {
            return Pending(editionId, operationId);
        }

        // An unobserved target outcome is never a receipt, including an ambiguous append.
        if (!producedFact && selected.RecordedGeneration is null)
            return Pending(editionId, operationId);
        return await receipts.GetOperationAsync(editionId, operationId, identifier, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<bool>> InspectOperationClaimAsync<TIntent>(Uuid editionId, string identifier,
        Uuid operationId, TIntent intent, ActorAttribution actor, Uuid userId, CancellationToken ct)
        where TIntent : notnull
    {
        var address = new CriteriaEntryOperation(editionId, identifier, operationId);
        try
        {
            var sourceClaim = await ReadClaimAsync(address, ct).ConfigureAwait(false);
            var owner = await aggregates.HydrateAsync(address, ct).ConfigureAwait(false);
            var ownerFact = (DomainEvent?)owner.ClaimFact ?? owner.SeedClaimFact;
            if (owner.CommittedStreamPosition != sourceClaim.NextResourceOffset ||
                (ownerFact?.Metadata.EventId ?? Uuid.Empty) != (sourceClaim.Claim?.Metadata.EventId ?? Uuid.Empty) ||
                !await freshHead!.IsCurrentAsync(address.Stream, address.Id, sourceClaim.NextResourceOffset, ct)
                    .ConfigureAwait(false))
                return ClaimUncertain();
            if (sourceClaim.Claim is null)
                return Result<bool>.Success(false);
            if (sourceClaim.Claim is not CriterionEntryOperationClaimed fact || fact.Actor != actor ||
                fact.Admission.UserId != userId ||
                !(intent is CriterionChangeIntent change && fact.Structural == change && fact.Summary is null ||
                  intent is CriterionSummaryIntent summary && fact.Summary == summary && fact.Structural is null))
                return Result<bool>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The operation identity is already bound to different intent or actor."));
            return Result<bool>.Success(true);
        }
        catch (CriteriaSourceUnavailableException)
        {
            return ClaimUncertain();
        }
        catch (FitzException)
        {
            return ClaimUncertain();
        }
        catch (IOException)
        {
            return ClaimUncertain();
        }
        catch (TimeoutException)
        {
            return ClaimUncertain();
        }
    }

    static Result<bool> ClaimUncertain() => Result<bool>.Failure(new RequestError(RequestErrorKind.Conflict,
        "The exact operation identity source is not yet qualified.", true));

    async ValueTask<Result<CriterionEntryOperationClaimed?>> EnsureOperationClaimAsync<TRequest, TIntent>(IRequestContext<TRequest> context,
        Uuid editionId, string identifier, Uuid operationId, TIntent intent, long execution,
        ActorAttribution actor, AccessAdmissionEvidence evidence, CancellationToken ct)
        where TRequest : IRequestBase where TIntent : notnull
    {
        var address = new CriteriaEntryOperation(editionId, identifier, operationId);
        try
        {
            var initial = await ReadClaimAsync(address, ct).ConfigureAwait(false);
            var observed = await aggregates.HydrateAsync(address, ct).ConfigureAwait(false);
            var observedFact = (DomainEvent?)observed.ClaimFact ?? observed.SeedClaimFact;
            if (observed.CommittedStreamPosition != initial.NextResourceOffset ||
                (observedFact?.Metadata.EventId ?? Uuid.Empty) != (initial.Claim?.Metadata.EventId ?? Uuid.Empty) ||
                !await freshHead!.IsCurrentAsync(address.Stream, address.Id, initial.NextResourceOffset, ct)
                    .ConfigureAwait(false))
                return Result<CriterionEntryOperationClaimed?>.Success(null);

            if (!await CriteriaAuthoringReadiness.CheckAsync(readiness, ct).ConfigureAwait(false))
                return Result<CriterionEntryOperationClaimed?>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Criteria authoring is currently unavailable.", true));
            var outcome = await executor.ExecuteAsync(new CriteriaEntryOperation(editionId, identifier, operationId), owner =>
            {
                // Do not rebase a different operation claim after a competing append.
                if (owner.CommittedStreamPosition != initial.NextResourceOffset)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The operation claim changed before it could be recorded.", true)));
                var result = intent switch
                {
                    CriterionChangeIntent structural => owner.Claim(structural, execution, actor, evidence),
                    CriterionSummaryIntent summary => owner.Claim(summary, execution, actor, evidence),
                    _ => throw new InvalidOperationException("Unsupported criteria entry operation intent."),
                };
                return AggregateOutcome.CommitOnSuccess(result);
            }, context, ct).ConfigureAwait(false);
            if (!outcome.IsSuccess)
                return outcome.Error!.IsTransient ? Result<CriterionEntryOperationClaimed?>.Success(null) :
                    Result<CriterionEntryOperationClaimed?>.Failure(outcome.Error);

            // Acknowledgement is not a source certificate. Re-read the one-event
            // owner and qualify its current head before allowing a target write.
            var confirmed = await ReadClaimAsync(address, ct).ConfigureAwait(false);
            if (confirmed.Claim is not { } current ||
                !await freshHead.IsCurrentAsync(address.Stream, address.Id, confirmed.NextResourceOffset, ct)
                    .ConfigureAwait(false))
                return Result<CriterionEntryOperationClaimed?>.Success(null);
            if (current is not CriterionEntryOperationClaimed fact)
                return Result<CriterionEntryOperationClaimed?>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The operation identity is already reserved for the configured seed."));
            if (fact.Actor != actor || fact.Admission.UserId != evidence.UserId ||
                !(intent is CriterionChangeIntent change && fact.Structural == change && fact.Summary is null ||
                  intent is CriterionSummaryIntent summary && fact.Summary == summary && fact.Structural is null))
                return Result<CriterionEntryOperationClaimed?>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The operation identity is already bound to different intent or actor."));
            return Result<CriterionEntryOperationClaimed?>.Success(fact);
        }
        catch (CriteriaSourceUnavailableException)
        {
            return Result<CriterionEntryOperationClaimed?>.Success(null);
        }
        catch (EventStreamConcurrencyException)
        {
            return Result<CriterionEntryOperationClaimed?>.Success(null);
        }
        catch (FitzException)
        {
            return Result<CriterionEntryOperationClaimed?>.Success(null);
        }
        catch (IOException)
        {
            return Result<CriterionEntryOperationClaimed?>.Success(null);
        }
        catch (TimeoutException)
        {
            return Result<CriterionEntryOperationClaimed?>.Success(null);
        }
    }

    async ValueTask<ClaimHistory> ReadClaimAsync(CriteriaEntryOperation owner, CancellationToken ct)
    {
        DomainEvent? claim = null;
        ulong next = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            if (record.Stream != owner.Stream || record.ResourceOffset != next || record.Event.Metadata.IsAudit ||
                record.Event.Metadata.AggregateId != owner.Id || record.Event.Metadata.EventId == Uuid.Empty ||
                record.Event is not (CriterionEntryOperationClaimed or CriterionEntryOperationSeedClaimed) || claim is not null)
                throw new InvalidOperationException("The operation claim source must contain at most one exact fact.");
            claim = record.Event;
            next = checked(record.ResourceOffset + 1);
        }
        return new ClaimHistory(next, claim);
    }

    async ValueTask<SelectedHistory> ReadSelectedAsync(CriteriaEntry owner, Uuid operationId, CancellationToken ct)
    {
        object? intent = null;
        ActorAttribution? actor = null;
        long? recordedGeneration = null;
        Uuid? parentAcceptance = null;
        CriterionSeedProposed? seedProposal = null;
        ulong seedArea = 0;
        ulong next = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            if (record.Stream != owner.Stream || record.ResourceOffset != next ||
                record.Event.Metadata.AggregateId != owner.Id || record.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The entry source is not a contiguous history of its exact owner.");
            next = checked(record.ResourceOffset + 1);
            if (record.Event is CriterionSeedProposed seed && seed.Intent.OperationId == operationId)
            {
                if (seedProposal is not null || intent is not null)
                    throw new InvalidOperationException("The entry source repeats a selected seed operation.");
                seedProposal = seed;
                seedArea = record.AreaOffset;
                continue;
            }
            var selected = record.Event switch
            {
                CriterionChangeProposed value when value.Intent.OperationId == operationId =>
                    (Intent: (object)value.Intent, value.Actor, Generation: (long?)value.ExecutionGeneration,
                        Parent: value.ParentAcceptedEventId),
                CriterionSummaryCorrected value when value.Intent.OperationId == operationId =>
                    (Intent: (object)value.Intent, value.Actor, Generation: (long?)value.ExecutionGeneration, Parent: (Uuid?)null),
                CriterionSummaryUnchanged value when value.Intent.OperationId == operationId =>
                    (Intent: (object)value.Intent, value.Actor, Generation: (long?)value.ExecutionGeneration, Parent: (Uuid?)null),
                CriterionSummaryRefused value when value.Intent.OperationId == operationId =>
                    (Intent: (object)value.Intent, value.Actor, Generation: (long?)value.ExecutionGeneration, Parent: (Uuid?)null),
                CriterionOperationAttemptFenced value when value.Intent.OperationId == operationId =>
                    (Intent: (object)value.Intent, value.Actor, Generation: (long?)null, Parent: (Uuid?)null),
                CriterionSummaryAttemptFenced value when value.Intent.OperationId == operationId =>
                    (Intent: (object)value.Intent, value.Actor, Generation: (long?)null, Parent: (Uuid?)null),
                _ => default,
            };
            if (selected.Intent is null)
                continue;
            if (seedProposal is not null || intent is not null && (!Equals(intent, selected.Intent) || actor != selected.Actor) ||
                recordedGeneration is not null && selected.Generation is not null)
                throw new InvalidOperationException("The entry source contains incompatible selected operations.");
            intent = selected.Intent;
            actor = selected.Actor;
            if (selected.Generation is not null)
            {
                recordedGeneration = selected.Generation;
                parentAcceptance = selected.Parent;
            }
        }
        return new SelectedHistory(next, intent, actor, recordedGeneration, parentAcceptance, seedProposal, seedArea);
    }

    async ValueTask<bool> DraftWasEstablishedAsync(Uuid editionId, Uuid generation, CancellationToken ct)
    {
        var edition = new CriteriaEdition(editionId);
        ulong next = 0;
        await foreach (var record in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
        {
            if (record.Stream != edition.Stream || record.ResourceOffset != next ||
                record.Event.Metadata.AggregateId != edition.Id || record.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The edition source is not a contiguous history of its exact owner.");
            next = checked(next + 1);
            if (record.Event is CriteriaEditionDrafted drafted && drafted.EditionId == editionId && drafted.Framework == "tsc" &&
                drafted.DraftGeneration == generation && drafted.Metadata.EventId != Uuid.Empty &&
                drafted.OperationId != Uuid.Empty && drafted.LabelGeneration > 0 && drafted.ConfirmationEventId != Uuid.Empty)
                return true;
        }
        // An unobserved draft may be delayed or hidden by an unqualified read.
        return false;
    }

    async ValueTask<Uuid?> FindParentAcceptanceAsync(CriterionChangeIntent change, CancellationToken ct)
    {
        var parent = new CriteriaEntry(change.EditionId, change.ParentIdentifier!);
        CriterionChangeIntent? proposalIntent = null;
        var proposalEventId = Uuid.Empty;
        long proposalGeneration = 0;
        ulong proposalArea = 0;
        CriteriaOrderedSourceEvent? proposalRecord = null;
        ulong next = 0;
        await foreach (var record in source.ReadAsync(parent.Stream, parent.Id, ct).ConfigureAwait(false))
        {
            if (record.Stream != parent.Stream || record.ResourceOffset != next ||
                record.Event.Metadata.AggregateId != parent.Id || record.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The parent source is not a contiguous history of its exact owner.");
            next = checked(next + 1);
            if (record.Event is CriterionChangeProposed candidate && candidate.IncarnationId == change.ParentIncarnationId &&
                candidate.Intent.EditionId == change.EditionId && candidate.Intent.DraftGeneration == change.DraftGeneration &&
                candidate.Intent.Identifier == change.ParentIdentifier && candidate.Intent.Kind == "criterion" &&
                candidate.Intent.Category == change.Category && candidate.Intent.Action is "create" or "replace")
            {
                proposalIntent = candidate.Intent;
                proposalEventId = candidate.Metadata.EventId;
                proposalGeneration = candidate.ExecutionGeneration;
                proposalArea = record.AreaOffset;
                if (!await new CriteriaHumanBindingVerifier(source).VerifyProposalAsync(candidate, record.AreaOffset, ct)
                        .ConfigureAwait(false))
                    return null;
                proposalRecord = record;
            }
            else if (record.Event is CriterionSeedProposed seed && seed.IncarnationId == change.ParentIncarnationId &&
                seed.Intent.EditionId == change.EditionId && seed.Intent.DraftGeneration == change.DraftGeneration &&
                seed.Intent.Identifier == change.ParentIdentifier && seed.Intent.Kind == "criterion" &&
                seed.Intent.Category == change.Category && seed.Intent.Action is "create" or "replace")
            {
                if (!await new CriteriaSeedBindingVerifier(source).VerifyProposalAsync(seed, record.AreaOffset, ct)
                        .ConfigureAwait(false))
                    return null;
                proposalIntent = seed.Intent;
                proposalEventId = seed.Metadata.EventId;
                proposalGeneration = seed.ExecutionGeneration;
                proposalArea = record.AreaOffset;
                proposalRecord = record;
            }
            else if (proposalIntent is not null && record.Event is CriterionChanged accepted &&
                     accepted.EditionId == change.EditionId && accepted.Identifier == change.ParentIdentifier &&
                     accepted.OperationId == proposalIntent.OperationId &&
                     accepted.ExecutionGeneration == proposalGeneration && accepted.ProposalEventId == proposalEventId &&
                     proposalEventId != Uuid.Empty && accepted.Metadata.EventId != Uuid.Empty &&
                     record.AreaOffset > proposalArea)
            {
                if (proposalRecord is null || sourceCoverage is null || publications is null ||
                    await new CriteriaEntryDecisionEvidenceVerifier(source, sourceCoverage, publications, causal)
                        .VerifyAsync(proposalRecord, record, ct).ConfigureAwait(false) !=
                    CriteriaEntryDecisionVerification.Verified)
                    return null;
                return accepted.Metadata.EventId;
            }
        }
        return null;
    }

    sealed record SelectedHistory(ulong NextResourceOffset, object? Intent, ActorAttribution? Actor,
        long? RecordedGeneration, Uuid? ParentAcceptedEventId, CriterionSeedProposed? SeedProposal, ulong SeedArea);

    sealed record ClaimHistory(ulong NextResourceOffset, DomainEvent? Claim);

    static Result<CriteriaOperationReceipt> Invalid() => Result<CriteriaOperationReceipt>.Failure(
        new RequestError(RequestErrorKind.Validation,
            "An edition, operation and canonical identifier and content within criteria size limits are required."));
    static Result<CriteriaOperationReceipt> Forbidden() => Result<CriteriaOperationReceipt>.Failure(
        new RequestError(RequestErrorKind.Forbidden, "Current platform operator authority is required."));
    static Result<CriteriaOperationReceipt> Unavailable() => Result<CriteriaOperationReceipt>.Failure(
        new RequestError(RequestErrorKind.Conflict, "Source-authoritative entry admission is unavailable.", true));
    static Result<CriteriaOperationReceipt> Conflict(string message) => Result<CriteriaOperationReceipt>.Failure(
        new RequestError(RequestErrorKind.Conflict, message));
    static Result<CriteriaOperationReceipt> Pending(Uuid editionId, Uuid operationId) =>
        Result<CriteriaOperationReceipt>.Success(new CriteriaOperationReceipt(editionId, operationId, "pending"));
}
