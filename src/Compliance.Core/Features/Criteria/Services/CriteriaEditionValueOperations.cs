using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Source-checked submission of one independent edition value proposal.</summary>
/// <remarks>
/// This service records proposals only. A separate source-qualified process must
/// decide each proposal against the edition cut; unknown source and ambiguous
/// appends remain pending. No default fresh-head provider grants write authority.
/// </remarks>
public sealed class CriteriaEditionValueOperations(IAggregateReader aggregates, IAggregateExecutor executor,
    ICriteriaExactSourceReader source, IAccessAdmissionAuthority? admission = null, ICriteriaFreshEntryHead? freshHead = null,
    ICriteriaSourceCutCoverage? coverage = null, ICriteriaPublicationAuthority? publications = null,
    ICriteriaParticipantCausalOrder? causalOrder = null, ICriteriaAuthoringReadiness? readiness = null)
{
    /// <summary>Submits one metadata field set or correction.</summary>
    public ValueTask<Result<CriteriaOperationReceipt>> SetFieldAsync(IRequestContext<SetCriteriaEditionField> context,
        CancellationToken ct)
    {
        var r = context.Request;
        return ExecuteAsync(context, new CriteriaEditionValueIntent(r.EditionId, r.DraftGeneration, "field", r.Field,
            null, null, r.OperationId, r.ExpectedRevision, r.TextValue, r.BooleanValue), ct);
    }

    /// <summary>Submits one support disclosure note set or correction.</summary>
    public ValueTask<Result<CriteriaOperationReceipt>> SetDisclosureAsync(IRequestContext<SetCriteriaEditionDisclosure> context,
        CancellationToken ct)
    {
        var r = context.Request;
        return ExecuteAsync(context, new CriteriaEditionValueIntent(r.EditionId, r.DraftGeneration, "disclosure", null,
            r.Category, r.Code, r.OperationId, r.ExpectedRevision, r.Note, null), ct);
    }

    async ValueTask<Result<CriteriaOperationReceipt>> ExecuteAsync<TRequest>(IRequestContext<TRequest> context,
        CriteriaEditionValueIntent intent, CancellationToken ct) where TRequest : IRequestBase
    {
        if (!CriteriaEditionValueRules.Shape(intent))
            return Invalid();
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Forbidden();
        if (admission is null || freshHead is null ||
            !await CriteriaAuthoringReadiness.CheckAsync(readiness, ct).ConfigureAwait(false))
            return Unavailable();
        var identity = context.Actor.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated &&
            candidate.FindFirst("iss")?.Value == "bdgrz");
        var subject = identity?.FindFirst("sub")?.Value;
        var issuer = identity?.FindFirst("iss")?.Value;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer))
            return Forbidden();
        var actor = new ActorAttribution(subject, issuer);
        var target = NewTarget(intent);

        try
        {
            // Until a target-fence recovery protocol is qualified, new executions
            // use generation one and later generations are never guessed.
            const long generation = 1;
            var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), generation);
            var evidence = await admission.AdmitCriteriaOperatorAsync(context, intent.OperationId,
                target.Stream.ToString(), digest, ct).ConfigureAwait(false);
            if (evidence is not null && !AccessAdmissionEvidenceBinding.IsCurrentFormat(evidence))
                return Unavailable();
            if (evidence is null || !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(evidence, userId, actor,
                    intent.OperationId, target.Stream.ToString(), digest))
                return Forbidden();
            if (!CriteriaContentBounds.AdmissionFields(actor, evidence))
                return Invalid();

            var targetSource = await ReadTargetAsync(target, intent.OperationId, ct).ConfigureAwait(false);
            var observed = await aggregates.HydrateAsync(target, ct).ConfigureAwait(false);
            if (observed.CommittedStreamPosition != targetSource.Next ||
                !await freshHead.IsCurrentAsync(target.Stream, target.Id, targetSource.Next, ct).ConfigureAwait(false))
                return Unavailable();
            if (!targetSource.OutcomesVerified)
                return Pending(intent);
            if (targetSource.Selected is { ExecutionGeneration: not 1 })
                return Pending(intent);

            var claimOwner = NewClaim(intent);
            var initial = await ReadClaimAsync(claimOwner, ct).ConfigureAwait(false);
            var observedClaim = await aggregates.HydrateAsync(claimOwner, ct).ConfigureAwait(false);
            if (observedClaim.CommittedStreamPosition != initial.Next ||
                (observedClaim.ClaimFact?.Metadata.EventId ?? Uuid.Empty) != (initial.Fact?.Metadata.EventId ?? Uuid.Empty) ||
                !await freshHead.IsCurrentAsync(claimOwner.Stream, claimOwner.Id, initial.Next, ct).ConfigureAwait(false))
                return Pending(intent);
            if (initial.Fact is { } prior && !Matches(prior, intent, actor, userId))
                return Conflict();
            if (initial.Fact is { ExecutionGeneration: not 1 })
                return Pending(intent);
            if (targetSource.Selected is { } selected && (selected.Intent != intent || selected.Actor != actor))
                return Conflict();

            var draft = await ReadDraftAsync(intent, ct).ConfigureAwait(false);
            if (draft is null)
                return Pending(intent);
            if (targetSource.Selected is { } existing)
            {
                // Source decision resolution requires a qualified publication cut
                // and remains separate. Never upgrade a source proposal to accepted here.
                if (initial.Fact is null || initial.Area <= draft.Value.DraftedArea || initial.Area >= targetSource.SelectedArea ||
                    draft.Value.DraftedArea >= targetSource.SelectedArea ||
                    existing.EditionRequestEventId != draft.Value.Request.Metadata.EventId ||
                    existing.OperationClaimEventId != initial.Fact.Metadata.EventId)
                    return Pending(intent);
                return Pending(intent);
            }
            if (observed.HasPendingChange || observed.Revision != intent.ExpectedRevision)
                return Pending(intent);

            if (initial.Fact is null)
            {
                if (!await CriteriaAuthoringReadiness.CheckAsync(readiness, ct).ConfigureAwait(false))
                    return Unavailable();
                var claimResult = await executor.ExecuteAsync(NewClaim(intent), owner =>
                {
                    if (owner.CommittedStreamPosition != initial.Next)
                        return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The operation claim changed before it was recorded.", true)));
                    return AggregateOutcome.CommitOnSuccess(owner.Claim(intent, generation, actor, evidence));
                }, context, ct).ConfigureAwait(false);
                if (!claimResult.IsSuccess)
                    return Pending(intent);
                initial = await ReadClaimAsync(claimOwner, ct).ConfigureAwait(false);
                if (initial.Fact is null ||
                    !await freshHead.IsCurrentAsync(claimOwner.Stream, claimOwner.Id, initial.Next, ct).ConfigureAwait(false))
                    return Pending(intent);
                if (!Matches(initial.Fact, intent, actor, userId))
                    return Conflict();
                if (initial.Fact.ExecutionGeneration != generation)
                    return Pending(intent);
            }
            if (initial.Area <= draft.Value.DraftedArea)
                return Pending(intent);
            var claimFact = initial.Fact;
            if (claimFact is null)
                return Pending(intent);

            if (!await CriteriaAuthoringReadiness.CheckAsync(readiness, ct).ConfigureAwait(false))
                return Pending(intent);
            var result = await executor.ExecuteAsync(NewTarget(intent), owner =>
            {
                if (owner.CommittedStreamPosition != targetSource.Next)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The value target changed before it was recorded.", true)));
                return AggregateOutcome.CommitOnSuccess(owner.Propose(intent, generation, actor, evidence,
                    draft.Value.Request, claimFact));
            }, context, ct).ConfigureAwait(false);
            _ = result;
            return Pending(intent);
        }
        catch (AccessAdmissionUnavailableException) { return Unavailable(); }
        catch (CriteriaSourceUnavailableException) { return Pending(intent); }
        catch (EventStreamConcurrencyException) { return Pending(intent); }
        catch (FitzException) { return Pending(intent); }
        catch (IOException) { return Pending(intent); }
        catch (TimeoutException) { return Pending(intent); }
    }

    async ValueTask<(CriteriaEditionDraftRequested Request, ulong DraftedArea)?> ReadDraftAsync(
        CriteriaEditionValueIntent intent, CancellationToken ct)
    {
        var owner = new CriteriaEdition(intent.EditionId);
        CriteriaEditionDraftRequested? request = null;
        ulong requestArea = 0;
        ulong next = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (record.Event is CriteriaEditionDraftRequested fact && fact.EditionId == intent.EditionId &&
                fact.DraftGeneration == intent.DraftGeneration && fact.OperationId == intent.DraftGeneration &&
                fact.Framework == "tsc" && fact.Admission is not null && fact.SeedVersion is null && fact.SeedFingerprint is null)
            {
                request = fact;
                requestArea = record.AreaOffset;
            }
            if (record.Event is CriteriaEditionDrafted drafted && request is not null &&
                drafted.EditionId == intent.EditionId && drafted.DraftGeneration == intent.DraftGeneration &&
                drafted.Framework == "tsc" && drafted.LabelGeneration > 0 && drafted.ConfirmationEventId != Uuid.Empty &&
                requestArea < record.AreaOffset)
                return (request, record.AreaOffset);
        }
        return null;
    }

    async ValueTask<(ulong Next, CriteriaEditionValueProposed? Selected, ulong SelectedArea, bool OutcomesVerified)> ReadTargetAsync(
        CriteriaEditionValue owner, Uuid operationId, CancellationToken ct)
    {
        CriteriaEditionValueProposed? selected = null;
        CriteriaOrderedSourceEvent? pending = null;
        var outcomesVerified = true;
        ulong selectedArea = 0, next = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (record.Event is CriteriaEditionValueProposed)
            {
                if (pending is not null)
                    throw new InvalidOperationException("A value target has concurrent proposals.");
                pending = record;
            }
            if (record.Event is CriteriaEditionValueAccepted or CriteriaEditionValueRefused)
            {
                if (pending is null)
                    throw new InvalidOperationException("A value outcome has no proposal.");
                if (coverage is null || publications is null || freshHead is null || causalOrder is null ||
                    await new CriteriaEditionValueDecisionEvidenceVerifier(source, coverage, publications, freshHead, causalOrder)
                        .VerifyAsync(pending, record, ct).ConfigureAwait(false) != CriteriaEntryDecisionVerification.Verified)
                    outcomesVerified = false;
                pending = null;
            }
            if (record.Event is CriteriaEditionValueProposed proposal && proposal.Intent.OperationId == operationId)
            {
                if (selected is not null)
                    throw new InvalidOperationException("A target operation has duplicate proposals.");
                selected = proposal;
                selectedArea = record.AreaOffset;
            }
        }
        return (next, selected, selectedArea, outcomesVerified);
    }

    async ValueTask<(ulong Next, CriteriaEditionValueOperationClaimed? Fact, ulong Area)> ReadClaimAsync(
        CriteriaEditionValueOperation owner, CancellationToken ct)
    {
        CriteriaEditionValueOperationClaimed? fact = null;
        ulong next = 0, area = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (record.Event is not CriteriaEditionValueOperationClaimed current || fact is not null)
                throw new InvalidOperationException("A value operation source must contain at most one exact claim.");
            fact = current;
            area = record.AreaOffset;
        }
        return (next, fact, area);
    }

    static void Check(CriteriaOrderedSourceEvent record, EventStreamAddress stream, Uuid id, ulong position)
    {
        if (record.Stream != stream || record.ResourceOffset != position || record.Event.Metadata.AggregateId != id ||
            record.Event.Metadata.EventId == Uuid.Empty || record.Event.Metadata.IsAudit ||
            record.Event.Metadata.AggregateVersion != checked(record.ResourceOffset + 1))
            throw new InvalidOperationException("A criteria source is not a contiguous exact owner history.");
    }

    static bool Matches(CriteriaEditionValueOperationClaimed claim, CriteriaEditionValueIntent intent,
        ActorAttribution actor, Uuid userId) => claim.Intent == intent && claim.Actor == actor && claim.Admission.UserId == userId;
    static CriteriaEditionValue NewTarget(CriteriaEditionValueIntent i) =>
        new(i.EditionId, i.TargetKind, i.Field, i.Category, i.Code, i.OperationId);
    static CriteriaEditionValueOperation NewClaim(CriteriaEditionValueIntent i) =>
        new(i.EditionId, i.TargetKind, i.Field, i.Category, i.Code, i.OperationId);
    static Result<CriteriaOperationReceipt> Pending(CriteriaEditionValueIntent i) =>
        Result<CriteriaOperationReceipt>.Success(new CriteriaOperationReceipt(i.EditionId, i.OperationId, "pending"));
    static Result<CriteriaOperationReceipt> Invalid() => Result<CriteriaOperationReceipt>.Failure(
        new RequestError(RequestErrorKind.Validation, "A bounded edition value and valid target are required."));
    static Result<CriteriaOperationReceipt> Forbidden() => Result<CriteriaOperationReceipt>.Failure(
        new RequestError(RequestErrorKind.Forbidden, "Current platform operator authority is required."));
    static Result<CriteriaOperationReceipt> Conflict() => Result<CriteriaOperationReceipt>.Failure(
        new RequestError(RequestErrorKind.Conflict, "The operation identity is already bound to different intent or actor."));
    static Result<CriteriaOperationReceipt> Unavailable() => Result<CriteriaOperationReceipt>.Failure(
        new RequestError(RequestErrorKind.Conflict, "Qualified criteria source authority is unavailable.", true));
}
