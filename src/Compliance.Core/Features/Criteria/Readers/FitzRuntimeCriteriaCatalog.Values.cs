using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Projections;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Readers;

sealed partial class FitzRuntimeCriteriaCatalog
{
    async ValueTask ApplyValueClaimAsync(DomainEventRecord source, RuntimeCriteriaEditionIndexRow edition,
        CriteriaEditionValueOperationClaimed claim, CancellationToken ct)
    {
        var intent = claim.Intent;
        if (!CriteriaEditionValueRules.Shape(intent) || intent.EditionId != edition.EditionId ||
            intent.DraftGeneration != edition.DraftGeneration ||
            !CriteriaEditionValueRules.Admission(intent, claim.ExecutionGeneration, claim.Actor, claim.Admission))
            throw new InvalidOperationException("A value claim must bind its exact edition and admitted intent.");
        var owner = new CriteriaEditionValueOperation(intent.EditionId, intent.TargetKind, intent.Field,
            intent.Category, intent.Code, intent.OperationId);
        if (source.Stream != owner.Stream || claim.Metadata.AggregateId != owner.Id ||
            claim.Metadata.AggregateVersion != 1)
            throw new InvalidOperationException("A value claim must come from its one-event operation owner.");
        var key = new RuntimeCriteriaProjectionSchema.ValueOwnerKey(_generation, owner.Id);
        if (await RuntimeCriteriaProjectionSchema.ValueClaims.GetAsync(Transaction, key, ct).ConfigureAwait(false) is not null)
            throw new InvalidOperationException("A value operation cannot have two claims.");
        await RuntimeCriteriaProjectionSchema.ValueClaims.InsertAsync(Transaction,
            new RuntimeCriteriaValueClaimIndexRow(_generation, owner.Id, claim.Metadata.EventId, intent,
                claim.ExecutionGeneration, claim.Actor, claim.Admission.UserId), ct).ConfigureAwait(false);
    }

    async ValueTask<RuntimeCriteriaEditionIndexRow> ApplyValueProposalAsync(DomainEventRecord source,
        RuntimeCriteriaEditionIndexRow edition, CriteriaEditionValueProposed proposed, CancellationToken ct)
    {
        var intent = proposed.Intent;
        if (!CriteriaEditionValueRules.Shape(intent) || intent.EditionId != edition.EditionId ||
            intent.DraftGeneration != edition.DraftGeneration || edition.SeedVersion is not null ||
            proposed.EditionRequestEventId != edition.DraftRequestEventId || proposed.OperationClaimEventId == Uuid.Empty ||
            !CriteriaEditionValueRules.Admission(intent, proposed.ExecutionGeneration, proposed.Actor, proposed.Admission))
            throw new InvalidOperationException("A value proposal must bind its draft request and admitted intent.");
        var target = new CriteriaEditionValue(intent.EditionId, intent.TargetKind, intent.Field, intent.Category, intent.Code);
        if (source.Stream != target.Stream || proposed.Metadata.AggregateId != target.Id)
            throw new InvalidOperationException("A value proposal must come from its exact owner.");
        var operation = new CriteriaEditionValueOperation(intent.EditionId, intent.TargetKind, intent.Field,
            intent.Category, intent.Code, intent.OperationId);
        var claim = await RuntimeCriteriaProjectionSchema.ValueClaims.GetAsync(Transaction,
            new RuntimeCriteriaProjectionSchema.ValueOwnerKey(_generation, operation.Id), ct).ConfigureAwait(false);
        var claimMatches = claim is not null && claim.EventId == proposed.OperationClaimEventId &&
            claim.Intent == intent && claim.Actor == proposed.Actor && claim.UserId == proposed.Admission.UserId &&
            claim.ExecutionGeneration == proposed.ExecutionGeneration;

        var key = new RuntimeCriteriaProjectionSchema.ValueOwnerKey(_generation, target.Id);
        if (await RuntimeCriteriaProjectionSchema.PendingValues.GetAsync(Transaction, key, ct).ConfigureAwait(false) is not null)
            throw new InvalidOperationException("A value owner cannot have two pending proposals.");
        // A request alone does not establish an active draft. Proposals made
        // before drafting or after publication cannot change published content.
        var beforePublication = edition.Status == "draft";
        await RuntimeCriteriaProjectionSchema.PendingValues.InsertAsync(Transaction,
            new RuntimeCriteriaValuePendingIndexRow(_generation, target.Id, intent, proposed.ExecutionGeneration,
                proposed.Metadata.EventId, beforePublication,
                claimMatches), ct).ConfigureAwait(false);
        return beforePublication ? edition with { PendingBeforeCut = checked(edition.PendingBeforeCut + 1) } : edition;
    }

    async ValueTask<RuntimeCriteriaEditionIndexRow> ApplyValueDecisionAsync(DomainEventRecord source,
        RuntimeCriteriaEditionIndexRow edition, Uuid operationId, long executionGeneration,
        Uuid proposalEventId, CriteriaEditionValueDecisionEvidence evidence, bool accepted, string? reason,
        CancellationToken ct)
    {
        var key = new RuntimeCriteriaProjectionSchema.ValueOwnerKey(_generation, source.Event.Metadata.AggregateId);
        var pending = await RuntimeCriteriaProjectionSchema.PendingValues.GetAsync(Transaction, key, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("A value outcome needs its exact proposal.");
        var intent = pending.Intent;
        var target = new CriteriaEditionValue(intent.EditionId, intent.TargetKind, intent.Field, intent.Category, intent.Code);
        if (source.Stream != target.Stream || target.Id != pending.OwnerId || intent.EditionId != edition.EditionId ||
            intent.OperationId != operationId || pending.ExecutionGeneration != executionGeneration ||
            pending.ProposalEventId != proposalEventId)
            throw new InvalidOperationException("The value outcome does not match its exact proposal.");
        // The realm projection checks already observed source facts and their
        // order. The provider must still qualify continuity and area positions.
        var sourceMatches = false;
        if (evidence is not null && evidence.EditionEventId != Uuid.Empty)
        {
            var head = await RuntimeCriteriaProjectionSchema.EditionFacts.GetAsync(Transaction,
                new RuntimeCriteriaProjectionSchema.EditionFactKey(_generation, edition.EditionId,
                    evidence.EditionEventId), ct).ConfigureAwait(false);
            var exactHead = head is not null && head.ResourceOffset == evidence.EditionResourceOffset;
            var published = exactHead && edition.Status == "published" &&
                edition.PublicationEventId == evidence.EditionEventId &&
                edition.PublicationResourceOffset == evidence.EditionResourceOffset;
            var abandoned = exactHead && edition.Status == "abandoned" &&
                edition.AbandonmentEventId == evidence.EditionEventId &&
                edition.AbandonmentResourceOffset == evidence.EditionResourceOffset;
            sourceMatches = evidence.Basis switch
            {
                "draft" => accepted && reason is null && pending.BeforePublication && exactHead && head!.IsActiveDraft &&
                    (edition.Status == "draft" || edition.Status == "published" && head.ObservationOrdinal < edition.PublicationOrdinal),
                "publication" => published && (accepted && pending.BeforePublication && reason is null ||
                    !accepted && !pending.BeforePublication && reason == "edition_published"),
                "abandonment" => abandoned && !accepted && reason == "edition_abandoned",
                _ => false,
            };
        }

        // A missing claim or an unqualified later execution cannot authorize
        // display of the proposed value, even as a partially covered edition.
        if (accepted && pending.ClaimGenerationMatches && sourceMatches)
            edition = await ApplyAcceptedValueAsync(edition, intent, ct).ConfigureAwait(false);
        await RuntimeCriteriaProjectionSchema.PendingValues.DeleteAsync(Transaction, pending, ct).ConfigureAwait(false);
        if (!pending.ClaimGenerationMatches || !sourceMatches)
            await RuntimeCriteriaProjectionSchema.UnresolvedValues.InsertAsync(Transaction,
                new RuntimeCriteriaValueUnresolvedIndexRow(_generation, source.Event.Metadata.EventId,
                    target.Id, source.ResourceOffset, intent, proposalEventId, evidence?.Basis ?? "unknown",
                    evidence?.EditionEventId ?? Uuid.Empty, evidence?.EditionResourceOffset ?? 0,
                    evidence?.EditionAreaOffset ?? 0, pending.ClaimGenerationMatches, sourceMatches), ct)
                .ConfigureAwait(false);
        if (!pending.BeforePublication || !pending.ClaimGenerationMatches || !sourceMatches)
            return edition;
        if (edition.PendingBeforeCut <= 0)
            throw new InvalidOperationException("The pending value count is inconsistent.");
        return edition with { PendingBeforeCut = edition.PendingBeforeCut - 1 };
    }

    async ValueTask<RuntimeCriteriaEditionIndexRow> ApplyAcceptedValueAsync(RuntimeCriteriaEditionIndexRow edition,
        CriteriaEditionValueIntent intent, CancellationToken ct)
    {
        if (intent.TargetKind == "field")
            return intent.Field switch
            {
                "coverage_note" => edition with { CoverageNote = intent.TextValue },
                "source_url" => edition with { SourceUrl = intent.TextValue },
                "content_rights" => edition with { ContentRights = intent.TextValue },
                "numbered_criteria_complete" => edition with { NumberedCriteriaComplete = intent.BooleanValue },
                _ => throw new InvalidOperationException("The metadata field is unknown."),
            };
        if (intent.TargetKind != "disclosure" || intent.Category is null || intent.Code is null || intent.TextValue is null)
            throw new InvalidOperationException("The disclosure value is invalid.");
        var key = new RuntimeCriteriaProjectionSchema.DisclosureKey(_generation, edition.EditionId, intent.Category, intent.Code);
        var current = await RuntimeCriteriaProjectionSchema.Disclosures.GetAsync(Transaction, key, ct).ConfigureAwait(false);
        var value = new RuntimeCriteriaDisclosureIndexRow(_generation, edition.EditionId, intent.Category, intent.Code, intent.TextValue);
        if (current is null)
            await RuntimeCriteriaProjectionSchema.Disclosures.InsertAsync(Transaction, value, ct).ConfigureAwait(false);
        else
            await RuntimeCriteriaProjectionSchema.Disclosures.ReplaceAsync(Transaction, current, value, ct).ConfigureAwait(false);
        return edition;
    }
}
