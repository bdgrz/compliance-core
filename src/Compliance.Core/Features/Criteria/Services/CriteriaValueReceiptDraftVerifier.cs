using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Verifies the exact admitted draft and its initial label participant chain.</summary>
sealed class CriteriaValueReceiptDraftVerifier(ICriteriaExactSourceReader source)
{
    internal async ValueTask<bool> VerifyAsync(CriteriaEditionValueProposed proposal, ulong claimArea, CancellationToken ct)
    {
        var intent = proposal.Intent;
        var edition = new CriteriaEdition(intent.EditionId);
        var requestRecord = await source.FindAsync(edition.Stream, edition.Id, proposal.EditionRequestEventId, ct).ConfigureAwait(false);
        if (requestRecord?.Event is not CriteriaEditionDraftRequested request || !Exact(requestRecord, edition.Stream, edition.Id) ||
            request.Metadata.EventId != proposal.EditionRequestEventId ||
            request.EditionId != intent.EditionId || request.DraftGeneration != intent.DraftGeneration ||
            request.OperationId != intent.DraftGeneration || request.Framework != "tsc" || request.ExecutionGeneration <= 0 ||
            request.SeedVersion is not null || request.SeedFingerprint is not null || request.Admission is null ||
            !CriteriaContentBounds.AdmissionFields(request.Actor, request.Admission))
            return false;
        var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(new DraftCriteriaEdition(
            request.EditionId, request.OperationId, request.Framework, request.Label)), request.ExecutionGeneration);
        if (!AccessAdmissionEvidenceBinding.BindsCriteriaOperator(request.Admission, request.Admission.UserId,
                request.Actor, request.OperationId, edition.Stream.ToString(), digest))
            return false;
        CriteriaOrderedSourceEvent? draftedRecord = null;
        ulong expected = 0;
        await foreach (var item in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
        {
            if (!Exact(item, edition.Stream, edition.Id) || item.ResourceOffset != expected++)
                return false;
            if (item.Event is CriteriaEditionDrafted draft && draft.EditionId == request.EditionId &&
                draft.DraftGeneration == request.DraftGeneration && draft.Framework == request.Framework)
            {
                if (draftedRecord is not null)
                    return false;
                draftedRecord = item;
            }
        }
        if (draftedRecord?.Event is not CriteriaEditionDrafted drafted || drafted.ConfirmationEventId == Uuid.Empty ||
            !EditionLabelClaim.TryNormalize(drafted.Label, out var label) || label != drafted.Label ||
            drafted.LabelGeneration <= 0 || requestRecord.AreaOffset >= draftedRecord.AreaOffset ||
            draftedRecord.AreaOffset >= claimArea)
            return false;
        var claim = new EditionLabelClaim(label);
        var confirmation = await source.FindAsync(claim.Stream, claim.Id, drafted.ConfirmationEventId, ct).ConfigureAwait(false);
        if (confirmation?.Event is not EditionLabelConfirmed confirmed || !Exact(confirmation, claim.Stream, claim.Id) ||
            confirmed.EditionId != request.EditionId || confirmed.OperationId != drafted.OperationId ||
            confirmed.Label != label || confirmed.Generation != drafted.LabelGeneration || confirmed.EditionAcceptedEventId == Uuid.Empty)
            return false;
        var activation = await source.FindAsync(edition.Stream, edition.Id, confirmed.EditionAcceptedEventId, ct).ConfigureAwait(false);
        if (activation?.Event is not CriteriaEditionDraftActivated active || !Exact(activation, edition.Stream, edition.Id) ||
            active.EditionId != request.EditionId || active.DraftGeneration != request.DraftGeneration ||
            active.Framework != request.Framework || active.OperationId != drafted.OperationId || active.Label != label ||
            active.LabelGeneration != drafted.LabelGeneration || active.ReservationEventId == Uuid.Empty ||
            activation.AreaOffset >= draftedRecord.AreaOffset)
            return false;
        var reservation = await source.FindAsync(claim.Stream, claim.Id, active.ReservationEventId, ct).ConfigureAwait(false);
        if (reservation?.Event is not EditionLabelReserved reserved || !Exact(reservation, claim.Stream, claim.Id) ||
            reserved.EditionId != request.EditionId || reserved.OperationId != drafted.OperationId || reserved.Label != label ||
            reserved.Generation != drafted.LabelGeneration || reserved.EditionRequestEventId == Uuid.Empty ||
            reservation.ResourceOffset >= confirmation.ResourceOffset)
            return false;
        var labelRequest = await source.FindAsync(edition.Stream, edition.Id, reserved.EditionRequestEventId, ct).ConfigureAwait(false);
        if (labelRequest is null || !Exact(labelRequest, edition.Stream, edition.Id) ||
            labelRequest.AreaOffset < requestRecord.AreaOffset || labelRequest.AreaOffset >= activation.AreaOffset)
            return false;
        if (labelRequest.Event is CriteriaEditionDraftRequested first)
            return first.Metadata.EventId == request.Metadata.EventId && first.OperationId == reserved.OperationId &&
                first.Label == label && first.Actor == reserved.RequestedBy;
        if (labelRequest.Event is not CriteriaInitialLabelRequested initial || initial.EditionId != request.EditionId ||
            initial.OperationId != reserved.OperationId || initial.Label != label || initial.Actor != reserved.RequestedBy ||
            initial.ExecutionGeneration <= 0 || initial.Admission is null ||
            !CriteriaContentBounds.AdmissionFields(initial.Actor, initial.Admission))
            return false;
        var initialDigest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(new ChooseInitialEditionLabel(
            initial.EditionId, initial.OperationId, initial.Label)), initial.ExecutionGeneration);
        return AccessAdmissionEvidenceBinding.BindsCriteriaOperator(initial.Admission, initial.Admission.UserId,
            initial.Actor, initial.OperationId, edition.Stream.ToString(), initialDigest);
    }

    static bool Exact(CriteriaOrderedSourceEvent item, EventStreamAddress stream, Uuid id) => item.Stream == stream &&
        item.Event.Metadata.AggregateId == id && item.Event.Metadata.EventId != Uuid.Empty && !item.Event.Metadata.IsAudit &&
        item.Event.Metadata.AggregateVersion == checked(item.ResourceOffset + 1);
}
