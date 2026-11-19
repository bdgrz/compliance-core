using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Resolves one exact value proposal against a source-qualified terminal edition cut.</summary>
/// <remarks>Null means pending. Positive source facts alone do not qualify source continuity.</remarks>
sealed class CriteriaEditionValueDecisionAuthority(ICriteriaExactSourceReader source,
    ICriteriaSourceCutCoverage coverage, ICriteriaPublicationAuthority publications, ICriteriaFreshEntryHead freshHead,
    ICriteriaParticipantCausalOrder? causalOrder = null)
{
    public async ValueTask<CriteriaEditionValueDecision?> DecideAsync(CriteriaOrderedSourceEvent proposalRecord,
        CancellationToken ct)
    {
        if (proposalRecord.Event is not CriteriaEditionValueProposed proposal ||
            !CriteriaEditionValueRules.Shape(proposal.Intent) ||
            !CriteriaEditionValueRules.Admission(proposal.Intent, proposal.ExecutionGeneration, proposal.Actor, proposal.Admission))
            throw new InvalidOperationException("The value proposal has invalid source intent or admission.");
        var i = proposal.Intent;
        var target = new CriteriaEditionValue(i.EditionId, i.TargetKind, i.Field, i.Category, i.Code);
        if (proposalRecord.Stream != target.Stream || proposal.Metadata.AggregateId != target.Id ||
            proposal.Metadata.IsAudit || proposal.Metadata.EventId == Uuid.Empty ||
            proposal.Metadata.AggregateVersion != checked(proposalRecord.ResourceOffset + 1))
            throw new InvalidOperationException("The proposal does not belong to its exact target source.");

        var claimOwner = new CriteriaEditionValueOperation(i.EditionId, i.TargetKind, i.Field, i.Category, i.Code, i.OperationId);
        var claimRecord = await source.FindAsync(claimOwner.Stream, claimOwner.Id, proposal.OperationClaimEventId, ct).ConfigureAwait(false);
        if (claimRecord is null)
            return null;
        if (claimRecord.Stream != claimOwner.Stream || claimRecord.Event is not CriteriaEditionValueOperationClaimed claim ||
            claim.Metadata.AggregateId != claimOwner.Id || claim.Metadata.EventId != proposal.OperationClaimEventId || claim.Metadata.IsAudit ||
            claim.Metadata.AggregateVersion != checked(claimRecord.ResourceOffset + 1) ||
            claim.Intent != i || claim.Actor != proposal.Actor || claim.Admission.UserId != proposal.Admission.UserId ||
            !CriteriaEditionValueRules.Admission(claim.Intent, claim.ExecutionGeneration, claim.Actor, claim.Admission))
            throw new InvalidOperationException("The value proposal does not match its exact operation claim.");
        // A later execution needs a separately qualified target fence.
        if (claim.ExecutionGeneration != proposal.ExecutionGeneration)
            return null;

        var edition = new CriteriaEdition(i.EditionId);
        CriteriaOrderedSourceEvent? request = null, drafted = null, terminal = null, head = null;
        var lifecycle = new CriteriaEntryEditionLifecycle(i.EditionId, i.DraftGeneration);
        var participants = new CriteriaValueEditionParticipantVerifier(source, coverage, causalOrder);
        ulong next = 0;
        await foreach (var record in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
        {
            if (record.Stream != edition.Stream || record.ResourceOffset != next++ ||
                record.Event.Metadata.AggregateId != edition.Id || record.Event.Metadata.IsAudit ||
                record.Event.Metadata.EventId == Uuid.Empty ||
                record.Event.Metadata.AggregateVersion != checked(record.ResourceOffset + 1))
                throw new InvalidOperationException("The edition source is not its exact contiguous history.");
            head = record;
            if (!lifecycle.Apply(record.Event))
                return null;
            if (!await participants.ObserveAsync(record, ct).ConfigureAwait(false))
                return null;
            if (record.Event is CriteriaEditionDraftRequested r && r.Metadata.EventId == proposal.EditionRequestEventId &&
                r.EditionId == i.EditionId && r.DraftGeneration == i.DraftGeneration && r.OperationId == i.DraftGeneration &&
                r.Framework == "tsc" && r.SeedVersion is null && r.SeedFingerprint is null && r.Admission is not null)
                request = record;
            if (record.Event is CriteriaEditionDrafted d && d.EditionId == i.EditionId &&
                d.DraftGeneration == i.DraftGeneration && d.Framework == "tsc" && d.LabelGeneration > 0 && d.ConfirmationEventId != Uuid.Empty)
                drafted = record;
            if (record.Event is CriteriaEditionPublished p && p.EditionId == i.EditionId && p.DraftGeneration == i.DraftGeneration ||
                record.Event is CriteriaEditionDraftAbandoned a && a.EditionId == i.EditionId && a.DraftGeneration == i.DraftGeneration)
            {
                if (terminal is not null)
                    throw new InvalidOperationException("An edition cannot have two terminal decisions.");
                terminal = record;
            }
        }
        if (request is null || drafted is null || head is null)
            return null;
        if (!(request.AreaOffset < drafted.AreaOffset && drafted.AreaOffset < claimRecord.AreaOffset &&
              claimRecord.AreaOffset < proposalRecord.AreaOffset))
            throw new InvalidOperationException("The draft, claim and proposal source positions are not ordered.");
        if (!await VerifyDraftBindingAsync(request, drafted, ct).ConfigureAwait(false) ||
            !await coverage.CoversAsync(edition.Stream, next, proposalRecord.AreaOffset, ct).ConfigureAwait(false))
            return null;
        if (terminal is null)
        {
            if (!lifecycle.ValueActive || !await freshHead.IsCurrentAsync(edition.Stream, edition.Id, next, ct).ConfigureAwait(false))
                return null;
            return new CriteriaEditionValueDecision(true, null, new CriteriaEditionValueDecisionEvidence("draft",
                head.Event.Metadata.EventId, head.ResourceOffset, head.AreaOffset, proposalRecord.AreaOffset));
        }
        if (terminal.AreaOffset <= drafted.AreaOffset)
            throw new InvalidOperationException("A terminal edition decision must follow its draft.");
        if (!await coverage.CoversAsync(edition.Stream, next, terminal.AreaOffset, ct).ConfigureAwait(false))
            return null;
        if (terminal.Event is CriteriaEditionDraftAbandoned abandoned)
            return CriteriaEntryDecisionSourceRules.ValidAbandonment(abandoned)
                ? new CriteriaEditionValueDecision(false, "edition_abandoned", new("abandonment", terminal.Event.Metadata.EventId,
                    terminal.ResourceOffset, terminal.AreaOffset, proposalRecord.AreaOffset)) : null;
        var published = await publications.ReadPublishedAsync(i.EditionId, ct).ConfigureAwait(false);
        if (!published.IsSuccess || published.Value is null)
            return null;
        if (published.Value.EventId != terminal.Event.Metadata.EventId || published.Value.ResourceOffset != terminal.ResourceOffset)
            throw new InvalidOperationException("The published edition evidence does not match its exact terminal source.");
        return proposalRecord.AreaOffset < terminal.AreaOffset
            ? new CriteriaEditionValueDecision(true, null, new("publication", terminal.Event.Metadata.EventId,
                terminal.ResourceOffset, terminal.AreaOffset, proposalRecord.AreaOffset))
            : new CriteriaEditionValueDecision(false, "edition_published", new("publication", terminal.Event.Metadata.EventId,
                terminal.ResourceOffset, terminal.AreaOffset, proposalRecord.AreaOffset));
    }

    internal async ValueTask<bool> VerifyDraftBindingAsync(CriteriaOrderedSourceEvent requestRecord,
        CriteriaOrderedSourceEvent draftedRecord, CancellationToken ct)
    {
        var request = (CriteriaEditionDraftRequested)requestRecord.Event;
        var drafted = (CriteriaEditionDrafted)draftedRecord.Event;
        if (request.Admission is not { } admission || !CriteriaContentBounds.AdmissionFields(request.Actor, admission) ||
            request.ExecutionGeneration <= 0 || !EditionLabelClaim.TryNormalize(drafted.Label, out var label) || label != drafted.Label ||
            !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(admission, admission.UserId, request.Actor,
                request.OperationId, new CriteriaEdition(request.EditionId).Stream.ToString(),
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(new DraftCriteriaEdition(request.EditionId,
                    request.OperationId, request.Framework, request.Label)), request.ExecutionGeneration)))
            return false;
        var claim = new EditionLabelClaim(label);
        var confirmation = await source.FindAsync(claim.Stream, claim.Id, drafted.ConfirmationEventId, ct).ConfigureAwait(false);
        if (confirmation?.Event is not EditionLabelConfirmed confirmed || !Exact(confirmation, claim.Stream, claim.Id) ||
            confirmed.Metadata.EventId != drafted.ConfirmationEventId ||
            confirmed.EditionId != drafted.EditionId || confirmed.OperationId != drafted.OperationId ||
            confirmed.Label != drafted.Label || confirmed.Generation != drafted.LabelGeneration)
            return false;
        var edition = new CriteriaEdition(request.EditionId);
        var activation = await source.FindAsync(edition.Stream, edition.Id, confirmed.EditionAcceptedEventId, ct).ConfigureAwait(false);
        if (activation?.Event is not CriteriaEditionDraftActivated active || !Exact(activation, edition.Stream, edition.Id) ||
            active.Metadata.EventId != confirmed.EditionAcceptedEventId ||
            active.EditionId != drafted.EditionId || active.DraftGeneration != drafted.DraftGeneration ||
            active.OperationId != drafted.OperationId || active.Framework != drafted.Framework || active.Label != drafted.Label ||
            active.LabelGeneration != drafted.LabelGeneration || activation.AreaOffset >= draftedRecord.AreaOffset)
            return false;
        var reservation = await source.FindAsync(claim.Stream, claim.Id, active.ReservationEventId, ct).ConfigureAwait(false);
        if (reservation?.Event is not EditionLabelReserved reserved || !Exact(reservation, claim.Stream, claim.Id) ||
            reserved.Metadata.EventId != active.ReservationEventId ||
            reserved.EditionId != drafted.EditionId || reserved.OperationId != drafted.OperationId ||
            reserved.Label != drafted.Label || reserved.Generation != drafted.LabelGeneration ||
            reservation.ResourceOffset >= confirmation.ResourceOffset)
            return false;
        var labelRequest = await source.FindAsync(edition.Stream, edition.Id, reserved.EditionRequestEventId, ct).ConfigureAwait(false);
        return labelRequest is not null && Exact(labelRequest, edition.Stream, edition.Id) &&
            labelRequest.Event.Metadata.EventId == reserved.EditionRequestEventId &&
            labelRequest.AreaOffset >= requestRecord.AreaOffset && labelRequest.AreaOffset < activation.AreaOffset &&
            causalOrder is not null &&
            await causalOrder.PrecedesAsync(labelRequest, reservation, ct).ConfigureAwait(false) &&
            await causalOrder.PrecedesAsync(reservation, activation, ct).ConfigureAwait(false) &&
            await causalOrder.PrecedesAsync(activation, confirmation, ct).ConfigureAwait(false) &&
            await causalOrder.PrecedesAsync(confirmation, draftedRecord, ct).ConfigureAwait(false) &&
            await new CriteriaValueEditionParticipantVerifier(source, coverage, causalOrder)
                .VerifyConfirmationHistoryAsync(claim, reservation, confirmation, ct).ConfigureAwait(false) &&
            (labelRequest.Event switch
            {
                CriteriaEditionDraftRequested initial => initial.EditionId == request.EditionId &&
                    initial.DraftGeneration == request.DraftGeneration && initial.OperationId == drafted.OperationId &&
                    initial.Label == drafted.Label && initial.Actor == reserved.RequestedBy,
                CriteriaInitialLabelRequested initial => initial.EditionId == request.EditionId &&
                    initial.OperationId == drafted.OperationId && initial.Label == drafted.Label && initial.Actor == reserved.RequestedBy,
                _ => false,
            });
    }

    static bool Exact(CriteriaOrderedSourceEvent item, EventStreamAddress stream, Uuid id) =>
        item.Stream == stream && item.Event.Metadata.AggregateId == id && !item.Event.Metadata.IsAudit &&
        item.Event.Metadata.EventId != Uuid.Empty && item.Event.Metadata.AggregateVersion == checked(item.ResourceOffset + 1);
}
