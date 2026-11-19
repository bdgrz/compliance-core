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

/// <summary>Decides one exact proposal using its own claim and qualified edition source.</summary>
/// <remarks>Null means pending; neither an unobserved tail nor a caller-provided identifier is authority.</remarks>
sealed class CriteriaEntryDecisionAuthority(ICriteriaExactSourceReader source,
    ICriteriaSourceCutCoverage coverage, ICriteriaPublicationAuthority publications, ICriteriaFreshEntryHead freshHead,
    ICriteriaParticipantCausalOrder? causal = null)
{
    public async ValueTask<CriteriaEntryDecision?> DecideAsync(CriteriaOrderedSourceEvent proposal, CancellationToken ct)
    {
        var intent = Intent(proposal.Event);
        if (intent is null || !CriteriaContentBounds.StructuralShape(intent))
            throw new InvalidOperationException("The proposal has an invalid structural intent.");
        var target = new CriteriaEntry(intent.EditionId, intent.Identifier);
        if (!Exact(proposal, target.Stream, target.Id))
            throw new InvalidOperationException("The proposal does not belong to its exact target.");
        var claimArea = await VerifyClaimAsync(source, proposal, ct).ConfigureAwait(false);
        if (claimArea is null)
            return null;

        var edition = new CriteriaEdition(intent.EditionId);
        CriteriaOrderedSourceEvent? request = null, drafted = null, terminal = null, head = null;
        var lifecycle = new CriteriaEntryEditionLifecycle(intent.EditionId, intent.DraftGeneration);
        var participants = new CriteriaValueEditionParticipantVerifier(source, coverage, causal);
        ulong next = 0;
        await foreach (var record in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
        {
            if (!Exact(record, edition.Stream, edition.Id) || record.ResourceOffset != next++)
                throw new InvalidOperationException("The edition source is not its exact contiguous history.");
            head = record;
            if (!lifecycle.Apply(record.Event) || !await participants.ObserveAsync(record, ct).ConfigureAwait(false))
                return null;
            if (record.Event is CriteriaEditionDraftRequested r && r.EditionId == intent.EditionId &&
                r.DraftGeneration == intent.DraftGeneration && r.OperationId == intent.DraftGeneration && r.Framework == "tsc")
                request = record;
            if (record.Event is CriteriaEditionDrafted d && d.EditionId == intent.EditionId &&
                d.DraftGeneration == intent.DraftGeneration && d.Framework == "tsc" && d.LabelGeneration > 0 &&
                d.ConfirmationEventId != Uuid.Empty)
                drafted = record;
            if (record.Event is CriteriaEditionPublished p && p.EditionId == intent.EditionId && p.DraftGeneration == intent.DraftGeneration ||
                record.Event is CriteriaEditionDraftAbandoned a && a.EditionId == intent.EditionId && a.DraftGeneration == intent.DraftGeneration)
            {
                if (terminal is not null)
                    throw new InvalidOperationException("The edition has multiple terminal decisions.");
                terminal = record;
            }
        }
        if (request is null || drafted is null || head is null ||
            !(request.AreaOffset < drafted.AreaOffset && drafted.AreaOffset < claimArea.Value && claimArea.Value < proposal.AreaOffset))
            return null;
        if (!await VerifyDraftBindingAsync(source, coverage, request, drafted, ct, causal).ConfigureAwait(false))
            return null;
        if (!await coverage.CoversAsync(edition.Stream, next, proposal.AreaOffset, ct).ConfigureAwait(false))
            return null;

        if (terminal is not null)
        {
            if (!await coverage.CoversAsync(edition.Stream, next, terminal.AreaOffset, ct).ConfigureAwait(false))
                return null;
            if (terminal.Event is CriteriaEditionDraftAbandoned abandoned)
                return CriteriaEntryDecisionSourceRules.ValidAbandonment(abandoned)
                    ? new CriteriaEntryDecision(false, "edition_abandoned", new("abandonment", terminal.Event.Metadata.EventId,
                        terminal.ResourceOffset, terminal.AreaOffset, proposal.AreaOffset)) : null;
            var published = await publications.ReadPublishedAsync(intent.EditionId, ct).ConfigureAwait(false);
            if (!published.IsSuccess || published.Value is null || published.Value.EventId != terminal.Event.Metadata.EventId ||
                published.Value.ResourceOffset != terminal.ResourceOffset)
                return null;
            if (proposal.AreaOffset > terminal.AreaOffset)
                return new CriteriaEntryDecision(false, "edition_published", new("publication", terminal.Event.Metadata.EventId,
                    terminal.ResourceOffset, terminal.AreaOffset, proposal.AreaOffset));
            if (proposal.AreaOffset == terminal.AreaOffset)
                return null;
        }

        var parentAccepted = proposal.Event switch
        {
            CriterionChangeProposed human => human.ParentAcceptedEventId,
            CriterionSeedProposed seed => seed.ParentAcceptedEventId,
            _ => null,
        };
        if (intent.Kind == "point_of_focus" && intent.Action != "remove" &&
            !await ParentActiveAsync(source, coverage, publications, intent, parentAccepted, proposal.AreaOffset, ct, causal).ConfigureAwait(false))
            return null;

        if (terminal is null)
        {
            if (!lifecycle.Active || !await freshHead.IsCurrentAsync(edition.Stream, edition.Id, next, ct).ConfigureAwait(false))
                return null;
            return new CriteriaEntryDecision(true, null, new("draft", head.Event.Metadata.EventId,
                head.ResourceOffset, head.AreaOffset, proposal.AreaOffset));
        }
        return new CriteriaEntryDecision(true, null, new("publication", terminal.Event.Metadata.EventId,
            terminal.ResourceOffset, terminal.AreaOffset, proposal.AreaOffset));
    }

    static async ValueTask<ulong?> VerifyClaimAsync(ICriteriaExactSourceReader source, CriteriaOrderedSourceEvent proposal, CancellationToken ct)
    {
        if (proposal.Event is CriterionSeedProposed seed)
            return await new CriteriaSeedBindingVerifier(source).VerifyProposalAsync(seed, proposal.AreaOffset, ct)
                .ConfigureAwait(false) ? await SeedClaimAreaAsync(source, seed, ct).ConfigureAwait(false) : null;
        if (proposal.Event is not CriterionChangeProposed human || human.OperationClaimEventId == Uuid.Empty ||
            human.ExecutionGeneration <= 0 || !CriteriaContentBounds.AdmissionFields(human.Actor, human.Admission))
            return null;
        var intent = human.Intent;
        var entry = new CriteriaEntry(intent.EditionId, intent.Identifier);
        var digest = CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), human.ExecutionGeneration);
        if (!AccessAdmissionEvidenceBinding.BindsCriteriaOperator(human.Admission, human.Admission.UserId, human.Actor,
                intent.OperationId, entry.Stream.ToString(), digest))
            return null;
        var operation = new CriteriaEntryOperation(intent.EditionId, intent.Identifier, intent.OperationId);
        var found = await source.FindAsync(operation.Stream, operation.Id, human.OperationClaimEventId, ct).ConfigureAwait(false);
        if (found is null)
            return null;
        if (!Exact(found, operation.Stream, operation.Id) || found.Event is not CriterionEntryOperationClaimed claim ||
            claim.Structural != intent || claim.Summary is not null || claim.Actor != human.Actor ||
            claim.Admission is null || claim.Admission.UserId != human.Admission.UserId ||
            claim.ExecutionGeneration <= 0 || claim.Metadata.EventId != human.OperationClaimEventId ||
            !CriteriaContentBounds.AdmissionFields(claim.Actor, claim.Admission) ||
            !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(claim.Admission, claim.Admission.UserId, claim.Actor,
                intent.OperationId, entry.Stream.ToString(),
                CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), claim.ExecutionGeneration)))
            throw new InvalidOperationException("The human proposal and its operation claim do not match.");
        // A later execution requires an independently source-verified target fence.
        return claim.ExecutionGeneration == human.ExecutionGeneration ? found.AreaOffset : null;
    }

    static async ValueTask<ulong?> SeedClaimAreaAsync(ICriteriaExactSourceReader source, CriterionSeedProposed seed, CancellationToken ct)
    {
        var intent = seed.Intent;
        var operation = new CriteriaEntryOperation(intent.EditionId, intent.Identifier, intent.OperationId);
        var claim = await source.FindAsync(operation.Stream, operation.Id, seed.OperationClaimEventId, ct).ConfigureAwait(false);
        return claim?.AreaOffset;
    }

    internal static async ValueTask<bool> VerifyDraftBindingAsync(ICriteriaExactSourceReader source, ICriteriaSourceCutCoverage coverage,
        CriteriaOrderedSourceEvent requestRecord,
        CriteriaOrderedSourceEvent draftedRecord, CancellationToken ct, ICriteriaParticipantCausalOrder? causal = null)
    {
        var request = (CriteriaEditionDraftRequested)requestRecord.Event;
        var drafted = (CriteriaEditionDrafted)draftedRecord.Event;
        if (request.DraftGeneration != drafted.DraftGeneration || request.EditionId != drafted.EditionId ||
            !EditionLabelClaim.TryNormalize(drafted.Label, out var label) || label != drafted.Label)
            return false;
        if (request.Admission is { } admission)
        {
            if (request.SeedVersion is not null || request.SeedFingerprint is not null ||
                !CriteriaContentBounds.AdmissionFields(request.Actor, admission) || request.ExecutionGeneration <= 0 ||
                !AccessAdmissionEvidenceBinding.BindsCriteriaOperator(admission, admission.UserId, request.Actor,
                    request.OperationId, new CriteriaEdition(request.EditionId).Stream.ToString(),
                    CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(new DraftCriteriaEdition(
                        request.EditionId, request.OperationId, request.Framework, request.Label)), request.ExecutionGeneration)))
                return false;
        }
        else if (request.Actor != new ActorAttribution("criteria-seed-bootstrap", "system") ||
                 !PlatformCriteriaSeedDefinition.MatchesBinding(request.EditionId, request.SeedVersion, request.SeedFingerprint))
            return false;
        var labelOwner = new EditionLabelClaim(label);
        var confirmation = await source.FindAsync(labelOwner.Stream, labelOwner.Id, drafted.ConfirmationEventId, ct)
            .ConfigureAwait(false);
        if (confirmation?.Event is not EditionLabelConfirmed confirmed || !Exact(confirmation, labelOwner.Stream, labelOwner.Id) ||
            confirmed.EditionId != drafted.EditionId || confirmed.OperationId != drafted.OperationId ||
            confirmed.Label != drafted.Label || confirmed.Generation != drafted.LabelGeneration)
            return false;
        var edition = new CriteriaEdition(request.EditionId);
        var activation = await source.FindAsync(edition.Stream, edition.Id, confirmed.EditionAcceptedEventId, ct).ConfigureAwait(false);
        if (activation?.Event is not CriteriaEditionDraftActivated active || !Exact(activation, edition.Stream, edition.Id) ||
            active.EditionId != drafted.EditionId || active.DraftGeneration != drafted.DraftGeneration ||
            active.OperationId != drafted.OperationId || active.Framework != drafted.Framework || active.Label != drafted.Label ||
            active.LabelGeneration != drafted.LabelGeneration || activation.AreaOffset >= draftedRecord.AreaOffset)
            return false;
        var reservation = await source.FindAsync(labelOwner.Stream, labelOwner.Id, active.ReservationEventId, ct).ConfigureAwait(false);
        if (reservation?.Event is not EditionLabelReserved reserved || !Exact(reservation, labelOwner.Stream, labelOwner.Id) ||
            reserved.EditionId != drafted.EditionId || reserved.OperationId != drafted.OperationId ||
            reserved.Label != drafted.Label || reserved.Generation != drafted.LabelGeneration ||
            reservation.ResourceOffset >= confirmation.ResourceOffset)
            return false;
        var labelRequest = await source.FindAsync(edition.Stream, edition.Id, reserved.EditionRequestEventId, ct).ConfigureAwait(false);
        return labelRequest is not null && Exact(labelRequest, edition.Stream, edition.Id) && causal is not null &&
               labelRequest.AreaOffset >= requestRecord.AreaOffset && labelRequest.AreaOffset < activation.AreaOffset &&
               await new CriteriaValueEditionParticipantVerifier(source, coverage, causal)
                   .VerifyConfirmationHistoryAsync(labelOwner, reservation, confirmation, ct).ConfigureAwait(false) &&
               await causal.PrecedesAsync(labelRequest, reservation, ct).ConfigureAwait(false) &&
               await causal.PrecedesAsync(reservation, activation, ct).ConfigureAwait(false) &&
               await causal.PrecedesAsync(activation, confirmation, ct).ConfigureAwait(false) &&
               await causal.PrecedesAsync(confirmation, draftedRecord, ct).ConfigureAwait(false) &&
               (labelRequest.Event switch
               {
                   CriteriaEditionDraftRequested initial => initial.EditionId == request.EditionId &&
                       initial.DraftGeneration == request.DraftGeneration && initial.OperationId == drafted.OperationId &&
                       initial.Label == drafted.Label && initial.Actor == reserved.RequestedBy,
                   CriteriaInitialLabelRequested initial => initial.EditionId == request.EditionId &&
                       initial.OperationId == drafted.OperationId && initial.Label == drafted.Label &&
                       initial.Actor == reserved.RequestedBy && ValidInitialLabelRequest(initial),
                   _ => false,
               });
    }

    static bool ValidInitialLabelRequest(CriteriaInitialLabelRequested initial) =>
        initial.OperationId != Uuid.Empty && initial.ExecutionGeneration > 0 && initial.Admission is not null &&
        CriteriaContentBounds.AdmissionFields(initial.Actor, initial.Admission) &&
        AccessAdmissionEvidenceBinding.BindsCriteriaOperator(initial.Admission, initial.Admission.UserId, initial.Actor,
            initial.OperationId, new CriteriaEdition(initial.EditionId).Stream.ToString(),
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(new ChooseInitialEditionLabel(initial.EditionId,
                initial.OperationId, initial.Label)), initial.ExecutionGeneration));

    internal static async ValueTask<bool> ParentActiveAsync(ICriteriaExactSourceReader source,
        ICriteriaSourceCutCoverage coverage, ICriteriaPublicationAuthority publications,
        CriterionChangeIntent child, Uuid? parentAcceptedEventId, ulong childArea, CancellationToken ct,
        ICriteriaParticipantCausalOrder? causal = null)
    {
        if (child.ParentIdentifier is null || child.ParentIncarnationId is null || child.Category is null ||
            parentAcceptedEventId is null)
            return false;
        var parent = new CriteriaEntry(child.EditionId, child.ParentIdentifier);
        CriteriaOrderedSourceEvent? pending = null, current = null, accepted = null;
        var verifier = new CriteriaEntryDecisionEvidenceVerifier(source, coverage, publications, causal);
        ulong next = 0;
        await foreach (var item in source.ReadAsync(parent.Stream, parent.Id, ct).ConfigureAwait(false))
        {
            if (!Exact(item, parent.Stream, parent.Id) || item.ResourceOffset != next++)
                throw new InvalidOperationException("The parent source is not an exact contiguous history.");
            var parentIntent = Intent(item.Event);
            if (parentIntent is not null && item.AreaOffset < childArea)
            {
                if (pending is not null)
                    throw new InvalidOperationException("The parent has two simultaneous proposals.");
                var claimArea = await VerifyClaimAsync(source, item, ct).ConfigureAwait(false);
                if (claimArea is null || claimArea.Value >= item.AreaOffset)
                    return false;
                pending = item;
                continue;
            }
            if (pending is null || item.Event is not (CriterionChanged or CriterionChangeRefused))
                continue;
            var pendingIntent = Intent(pending.Event)!;
            var outcomeId = item.Event switch { CriterionChanged x => x.ProposalEventId, CriterionChangeRefused x => x.ProposalEventId, _ => Uuid.Empty };
            if (outcomeId != pending.Event.Metadata.EventId)
                continue;
            if (await verifier.VerifyAsync(pending, item, ct).ConfigureAwait(false) != CriteriaEntryDecisionVerification.Verified)
                return false;
            if (item.Event is CriterionChanged)
            {
                current = pendingIntent.Action == "remove" ? null : pending;
                accepted = pendingIntent.Action == "remove" ? null : item;
            }
            pending = null;
        }
        if (pending is not null || !await coverage.CoversAsync(parent.Stream, next, childArea, ct).ConfigureAwait(false) ||
            current is null || accepted is null)
            return false;
        var currentIntent = Intent(current.Event)!;
        var incarnation = current.Event switch { CriterionChangeProposed h => h.IncarnationId, CriterionSeedProposed s => s.IncarnationId, _ => Uuid.Empty };
        return currentIntent.Kind == "criterion" && currentIntent.Category == child.Category &&
               incarnation == child.ParentIncarnationId && accepted.Event.Metadata.EventId == parentAcceptedEventId &&
               accepted.AreaOffset < childArea;
    }

    static CriterionChangeIntent? Intent(DomainEvent value) => value switch
    {
        CriterionChangeProposed human => human.Intent,
        CriterionSeedProposed seed => seed.Intent,
        _ => null,
    };
    static bool Exact(CriteriaOrderedSourceEvent item, EventStreamAddress stream, Uuid id) => item.Stream == stream &&
        item.Event.Metadata.AggregateId == id && item.Event.Metadata.EventId != Uuid.Empty && !item.Event.Metadata.IsAudit &&
        item.Event.Metadata.AggregateVersion == checked(item.ResourceOffset + 1);
}
