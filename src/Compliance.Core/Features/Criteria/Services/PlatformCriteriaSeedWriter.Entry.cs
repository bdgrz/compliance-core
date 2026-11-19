using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

sealed partial class PlatformCriteriaSeedWriter
{
    async ValueTask<CriteriaSeedWriteResult> ProposeSeedEntryAsync(CriteriaSeedHeader header, Uuid bindingId,
        Criterion entry, CriteriaEntry target, CancellationToken ct)
    {
        if (coverage is null || publications is null || causalOrder is null || editionHead is null || seedFactHead is null)
            return Pending;
        var draft = await ReadQualifiedSeedDraftAsync(header, bindingId, ct).ConfigureAwait(false);
        if (draft is null)
            return Pending;
        var request = draft.Value.Request;

        Uuid? parentIncarnation = null, parentAccepted = null;
        if (entry.Kind == "point_of_focus")
        {
            if (entry.ParentIdentifier is not { Length: > 0 } parent)
                return Pending;
            var parentOperation = PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "entry:" + parent);
            parentIncarnation = CriteriaEntry.IncarnationOf(header.EditionId, parent, parentOperation);
            var acceptance = await new CriteriaParentAcceptanceReader(source, coverage, publications, causalOrder)
                .ReadAsync(header.EditionId, parent, parentIncarnation.Value, entry.Category, ct).ConfigureAwait(false);
            if (!acceptance.IsSuccess || acceptance.Value is null)
                return Pending;
            parentAccepted = acceptance.Value.AcceptanceEventId;
            var parentOwner = new CriteriaEntry(header.EditionId, parent);
            ulong parentNext = 0;
            await foreach (var item in source.ReadAsync(parentOwner.Stream, parentOwner.Id, ct).ConfigureAwait(false))
                Check(item, parentOwner.Stream, parentOwner.Id, parentNext++);
            var currentParent = await aggregates.HydrateAsync(parentOwner, ct).ConfigureAwait(false);
            if (currentParent.CommittedStreamPosition != parentNext || currentParent.HasPendingChange ||
                currentParent.CurrentIncarnationId != parentIncarnation || currentParent.CurrentAcceptedEventId != parentAccepted ||
                currentParent.CurrentKind != "criterion" || currentParent.CurrentCategory != entry.Category ||
                !await seedFactHead.IsCurrentAsync(parentOwner.Stream, parentOwner.Id, parentNext, ct).ConfigureAwait(false))
                return Pending;
        }

        var operationId = PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "entry:" + entry.Identifier);
        var intent = new CriterionChangeIntent(header.EditionId, request.DraftGeneration, entry.Identifier, operationId,
            0, "create", entry.Kind, entry.Category, entry.ParentIdentifier, parentIncarnation, entry.Summary);
        var claimOwner = new CriteriaEntryOperation(header.EditionId, entry.Identifier, operationId);
        var claim = await ReadSeedClaimAsync(claimOwner, ct).ConfigureAwait(false);
        var hydratedClaim = await aggregates.HydrateAsync(claimOwner, ct).ConfigureAwait(false);
        if (hydratedClaim.CommittedStreamPosition != claim.Next ||
            (hydratedClaim.SeedClaimFact?.Metadata.EventId ?? Uuid.Empty) != (claim.Fact?.Metadata.EventId ?? Uuid.Empty) ||
            !await seedFactHead.IsCurrentAsync(claimOwner.Stream, claimOwner.Id, claim.Next, ct).ConfigureAwait(false))
            return Pending;
        if (claim.Fact is null && claim.Next != 0)
            return Pending;
        var incarnation = CriteriaEntry.IncarnationOf(header.EditionId, entry.Identifier, operationId);
        if (claim.Fact is null)
        {
            var result = await executor.ExecuteAsync(new CriteriaEntryOperation(header.EditionId, entry.Identifier, operationId), current =>
            {
                if (current.CommittedStreamPosition != claim.Next)
                    return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The seed operation claim changed before append.", true)));
                return AggregateOutcome.CommitOnSuccess(current.ClaimSeed(intent, 1, incarnation, parentAccepted, request));
            }, context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return Pending;
            claim = await ReadSeedClaimAsync(claimOwner, ct).ConfigureAwait(false);
            if (!await seedFactHead.IsCurrentAsync(claimOwner.Stream, claimOwner.Id, claim.Next, ct).ConfigureAwait(false))
                return Pending;
        }
        var recorded = claim.Fact;
        if (recorded is null || recorded.Intent != intent || recorded.ExecutionGeneration != 1 ||
            recorded.IncarnationId != incarnation || recorded.ParentAcceptedEventId != parentAccepted ||
            recorded.EditionRequestEventId != bindingId || recorded.SeedVersion != header.SeedVersion ||
            recorded.SeedFingerprint != header.SeedFingerprint || claim.Area <= draft.Value.DraftedArea)
            return Pending;

        var targetHistory = await ReadEntryAsync(target, operationId, ct).ConfigureAwait(false);
        if (targetHistory.Foreign || targetHistory.Proposal is not null)
            return Pending;
        var hydrated = await aggregates.HydrateAsync(target, ct).ConfigureAwait(false);
        if (hydrated.CommittedStreamPosition != targetHistory.Next || hydrated.HasPendingChange || hydrated.Revision != 0 ||
            !await seedFactHead.IsCurrentAsync(target.Stream, target.Id, targetHistory.Next, ct).ConfigureAwait(false))
            return Pending;
        var appended = await executor.ExecuteAsync(new CriteriaEntry(header.EditionId, entry.Identifier, operationId), current =>
        {
            if (current.CommittedStreamPosition != targetHistory.Next)
                return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The seed entry changed before append.", true)));
            return AggregateOutcome.CommitOnSuccess(current.ProposeSeed(intent, 1, parentAccepted, request, recorded.Metadata.EventId));
        }, context, ct).ConfigureAwait(false);
        _ = appended;
        // A successful proposal is not a participant outcome.
        return Pending;
    }

    async ValueTask<(ulong Next, CriterionEntryOperationSeedClaimed? Fact, ulong Area)> ReadSeedClaimAsync(CriteriaEntryOperation owner,
        CancellationToken ct)
    {
        ulong next = 0;
        ulong area = 0;
        CriterionEntryOperationSeedClaimed? fact = null;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (next != 1 || record.Event is not CriterionEntryOperationSeedClaimed seed)
                return (next, null, 0);
            fact = seed;
            area = record.AreaOffset;
        }
        return (next, fact, area);
    }

    async ValueTask<(CriteriaEditionDraftRequested Request, ulong DraftedArea, ulong EditionNext)?> ReadQualifiedSeedDraftAsync(CriteriaSeedHeader header, Uuid bindingId,
        CancellationToken ct)
    {
        if (coverage is null || causalOrder is null || editionHead is null)
            return null;
        var owner = new CriteriaEdition(header.EditionId);
        CriteriaOrderedSourceEvent? request = null, drafted = null, head = null;
        var lifecycle = new CriteriaEntryEditionLifecycle(header.EditionId,
            PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "edition"));
        var participants = new CriteriaValueEditionParticipantVerifier(source, coverage, causalOrder);
        ulong next = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            Check(record, owner.Stream, owner.Id, next++);
            if (!lifecycle.Apply(record.Event) || !await participants.ObserveAsync(record, ct).ConfigureAwait(false))
                return null;
            head = record;
            if (record.Event is CriteriaEditionDraftRequested r && r.Metadata.EventId == bindingId)
                request = record;
            if (record.Event is CriteriaEditionDrafted)
                drafted = record;
        }
        if (request?.Event is not CriteriaEditionDraftRequested binding || drafted is null || head is null || !lifecycle.Active ||
            request.AreaOffset >= drafted.AreaOffset ||
            !await editionHead.IsCurrentAsync(owner.Stream, owner.Id, next, ct).ConfigureAwait(false) ||
            !await coverage.CoversAsync(owner.Stream, next, head.AreaOffset, ct).ConfigureAwait(false) ||
            !await CriteriaEntryDecisionAuthority.VerifyDraftBindingAsync(source, coverage, request, drafted, ct, causalOrder)
                .ConfigureAwait(false))
            return null;
        return (binding, drafted.AreaOffset, next);
    }

    async ValueTask<CriteriaSeedWriteResult> StartSeedPublicationAsync(CriteriaSeedHeader header, Uuid bindingId,
        Uuid attemptId, string witnessIdentifier, CancellationToken ct)
    {
        if (coverage is null || publications is null || causalOrder is null || editionHead is null ||
            seedFactHead is null || seedLabelHead is null)
            return Pending;
        Criterion? witness = null;
        foreach (var candidate in PlatformCriteriaCatalog.EnumerateSeedEntries())
        {
            if (candidate.Identifier == witnessIdentifier && candidate.Kind == "criterion")
            {
                witness = candidate;
                break;
            }
        }
        if (witness is null)
            return Pending;
        var draft = await ReadQualifiedSeedDraftAsync(header, bindingId, ct).ConfigureAwait(false);
        if (draft is null)
            return Pending;
        var witnessOperation = PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "entry:" + witness.Identifier);
        var witnessOwner = new CriteriaEntry(header.EditionId, witness.Identifier, witnessOperation);
        var witnessHistory = await ReadEntryAsync(witnessOwner, witnessOperation, ct).ConfigureAwait(false);
        if (witnessHistory.Foreign || witnessHistory.Proposal is null)
            return Pending;
        var verifiedWitness = await EntryAsync(header, bindingId, witnessOperation, witness, ct).ConfigureAwait(false);
        if (verifiedWitness.Status != CriteriaSeedWriteStatus.Confirmed)
            return Pending;
        var loadedWitness = await aggregates.HydrateAsync(witnessOwner, ct).ConfigureAwait(false);
        var incarnation = CriteriaEntry.IncarnationOf(header.EditionId, witness.Identifier, witnessOperation);
        if (loadedWitness.CommittedStreamPosition != witnessHistory.Next || loadedWitness.HasPendingChange ||
            loadedWitness.IsWitnessPinned || loadedWitness.CurrentKind != "criterion" ||
            loadedWitness.CurrentIncarnationId != incarnation || loadedWitness.CurrentAcceptedEventId != verifiedWitness.EvidenceId ||
            !await seedFactHead.IsCurrentAsync(witnessOwner.Stream, witnessOwner.Id, witnessHistory.Next, ct).ConfigureAwait(false))
            return Pending;

        var editionOwner = new CriteriaEdition(header.EditionId, attemptId);
        var loadedEdition = await aggregates.HydrateAsync(editionOwner, ct).ConfigureAwait(false);
        if (loadedEdition.CommittedStreamPosition != draft.Value.EditionNext || loadedEdition.Status != "draft" ||
            loadedEdition.NextExecutionGeneration != 1 || loadedEdition.Framework != header.Framework ||
            loadedEdition.DraftGeneration != draft.Value.Request.DraftGeneration || loadedEdition.SeedVersion != header.SeedVersion ||
            loadedEdition.SeedFingerprint != header.SeedFingerprint || loadedEdition.Label is not { Length: > 0 } label ||
            loadedEdition.CurrentLabelOperationId == Uuid.Empty || loadedEdition.LabelGeneration <= 0 ||
            loadedEdition.LastLabelConfirmationEventId == Uuid.Empty)
            return Pending;
        var labelOwner = new EditionLabelClaim(label, header.EditionId, loadedEdition.CurrentLabelOperationId);
        ulong labelNext = 0;
        await foreach (var item in source.ReadAsync(labelOwner.Stream, labelOwner.Id, ct).ConfigureAwait(false))
            Check(item, labelOwner.Stream, labelOwner.Id, labelNext++);
        var loadedLabel = await aggregates.HydrateAsync(labelOwner, ct).ConfigureAwait(false);
        if (loadedLabel.CommittedStreamPosition != labelNext ||
            !loadedLabel.IsHeldBy(header.EditionId, loadedEdition.CurrentLabelOperationId, loadedEdition.LabelGeneration) ||
            loadedLabel.CurrentConfirmation?.Metadata.EventId != loadedEdition.LastLabelConfirmationEventId ||
            !await seedLabelHead.IsCurrentAsync(labelOwner.Stream, labelOwner.Id, labelNext, ct).ConfigureAwait(false) ||
            !await editionHead.IsCurrentAsync(editionOwner.Stream, editionOwner.Id, draft.Value.EditionNext, ct).ConfigureAwait(false))
            return Pending;

        var result = await executor.ExecuteAsync(new CriteriaEdition(header.EditionId, attemptId), current =>
        {
            if (current.CommittedStreamPosition != draft.Value.EditionNext || current.Status != "draft" ||
                current.LastLabelConfirmationEventId != loadedEdition.LastLabelConfirmationEventId)
                return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The edition changed before seed publication start.", true)));
            return AggregateOutcome.CommitOnSuccess(current.BeginSeedPublication(attemptId, witness.Identifier, incarnation));
        }, context, ct).ConfigureAwait(false);
        _ = result;
        // Start is not publication: the participant worker must pin the witness,
        // recheck the claim and commit an independently verified terminal fact.
        return Pending;
    }
}
