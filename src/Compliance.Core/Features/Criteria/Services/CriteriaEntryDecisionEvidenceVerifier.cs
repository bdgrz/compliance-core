using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Revalidates one committed entry outcome against exact source ordering.</summary>
/// <remarks>
/// An observed draft head is not proof it was current. For a draft-based outcome,
/// this verifier requires complete edition-source coverage through the actual
/// committed outcome position and checks every intervening terminal decision.
/// It streams one edition source and never reads its children.
/// </remarks>
sealed class CriteriaEntryDecisionEvidenceVerifier(ICriteriaExactSourceReader source,
    ICriteriaSourceCutCoverage coverage, ICriteriaPublicationAuthority publications, ICriteriaParticipantCausalOrder? causal = null)
{
    public async ValueTask<CriteriaEntryDecisionVerification> VerifyAsync(CriteriaOrderedSourceEvent proposalRecord,
        CriteriaOrderedSourceEvent decisionRecord, CancellationToken ct)
    {
        var intent = proposalRecord.Event switch
        {
            CriterionChangeProposed human => human.Intent,
            CriterionSeedProposed seed => seed.Intent,
            _ => null,
        };
        if (intent is null || intent.EditionId == Uuid.Empty || intent.DraftGeneration == Uuid.Empty ||
            string.IsNullOrWhiteSpace(intent.Identifier))
            return CriteriaEntryDecisionVerification.Pending;
        var owner = new CriteriaEntry(intent.EditionId, intent.Identifier);
        if (!Exact(proposalRecord, owner.Stream, owner.Id) || !Exact(decisionRecord, owner.Stream, owner.Id) ||
            proposalRecord.AreaOffset >= decisionRecord.AreaOffset || proposalRecord.ResourceOffset >= decisionRecord.ResourceOffset)
            return CriteriaEntryDecisionVerification.Pending;

        var (accepted, reason, evidence, operation, generation, proposedId) = decisionRecord.Event switch
        {
            CriterionChanged changed => (true, (string?)null, (CriteriaEntryDecisionEvidence?)changed.Evidence, changed.OperationId,
                changed.ExecutionGeneration, changed.ProposalEventId),
            CriterionChangeRefused refused => (false, (string?)refused.Reason, (CriteriaEntryDecisionEvidence?)refused.Evidence, refused.OperationId,
                refused.ExecutionGeneration, refused.ProposalEventId),
            _ => (false, (string?)null, (CriteriaEntryDecisionEvidence?)null, Uuid.Empty, 0L, Uuid.Empty),
        };
        var proposalGeneration = proposalRecord.Event switch
        {
            CriterionChangeProposed human => human.ExecutionGeneration,
            CriterionSeedProposed seed => seed.ExecutionGeneration,
            _ => 0L,
        };
        if (evidence is null || evidence.EditionEventId == Uuid.Empty || operation != intent.OperationId ||
            proposedId != proposalRecord.Event.Metadata.EventId || generation != proposalGeneration || generation <= 0 ||
            evidence.ProposalAreaOffset != proposalRecord.AreaOffset || evidence.EditionAreaOffset >= decisionRecord.AreaOffset ||
            evidence.EditionAreaOffset == proposalRecord.AreaOffset)
            return CriteriaEntryDecisionVerification.Pending;
        if (decisionRecord.Event is CriterionChanged changedOwner &&
                (changedOwner.EditionId != intent.EditionId || changedOwner.Identifier != intent.Identifier) ||
            decisionRecord.Event is CriterionChangeRefused refusedOwner &&
                (refusedOwner.EditionId != intent.EditionId || refusedOwner.Identifier != intent.Identifier))
            return CriteriaEntryDecisionVerification.Pending;

        // Resolve both named target facts again; caller-supplied records are not authority.
        var actualProposal = await source.FindAsync(owner.Stream, owner.Id, proposalRecord.Event.Metadata.EventId, ct)
            .ConfigureAwait(false);
        var actualDecision = await source.FindAsync(owner.Stream, owner.Id, decisionRecord.Event.Metadata.EventId, ct)
            .ConfigureAwait(false);
        if (!Same(actualProposal, proposalRecord) || !Same(actualDecision, decisionRecord))
            return CriteriaEntryDecisionVerification.Pending;

        var edition = new CriteriaEdition(intent.EditionId);
        CriteriaOrderedSourceEvent? request = null, drafted = null, named = null, terminal = null;
        var lifecycle = new CriteriaEntryEditionLifecycle(intent.EditionId, intent.DraftGeneration);
        var participants = new CriteriaValueEditionParticipantVerifier(source, coverage, causal);
        var namedActiveDraft = false;
        ulong next = 0;
        await foreach (var item in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
        {
            if (!Exact(item, edition.Stream, edition.Id) || item.ResourceOffset != next++)
                throw new InvalidOperationException("The edition source is not a contiguous exact history.");
            if (!lifecycle.Apply(item.Event) || !await participants.ObserveAsync(item, ct).ConfigureAwait(false))
                return CriteriaEntryDecisionVerification.Pending;
            if (item.Event is CriteriaEditionDraftRequested draftRequest && draftRequest.EditionId == intent.EditionId &&
                draftRequest.DraftGeneration == intent.DraftGeneration && draftRequest.OperationId == intent.DraftGeneration &&
                draftRequest.Framework == "tsc")
                request = item;
            if (item.Event is CriteriaEditionDrafted draft && draft.EditionId == intent.EditionId &&
                draft.DraftGeneration == intent.DraftGeneration && draft.Framework == "tsc")
                drafted = item;
            if (item.Event.Metadata.EventId == evidence.EditionEventId)
            {
                named = item;
                namedActiveDraft = lifecycle.Active;
            }
            if (item.AreaOffset >= decisionRecord.AreaOffset)
                continue;
            if (item.Event is CriteriaEditionPublished published && published.EditionId == intent.EditionId &&
                    published.DraftGeneration == intent.DraftGeneration ||
                item.Event is CriteriaEditionDraftAbandoned abandoned && abandoned.EditionId == intent.EditionId &&
                    abandoned.DraftGeneration == intent.DraftGeneration)
            {
                if (terminal is not null)
                    throw new InvalidOperationException("An edition generation has multiple terminal facts.");
                terminal = item;
            }
        }
        if (request is null || drafted is null || named is null || request.AreaOffset >= drafted.AreaOffset ||
            drafted.AreaOffset >= proposalRecord.AreaOffset || terminal is not null && terminal.AreaOffset <= drafted.AreaOffset ||
            named.ResourceOffset != evidence.EditionResourceOffset || named.AreaOffset != evidence.EditionAreaOffset ||
            named.ResourceOffset < drafted.ResourceOffset ||
            !await coverage.CoversAsync(edition.Stream, next, decisionRecord.AreaOffset, ct).ConfigureAwait(false))
            return CriteriaEntryDecisionVerification.Pending;
        if (!await CriteriaEntryDecisionAuthority.VerifyDraftBindingAsync(source, coverage, request, drafted, ct, causal).ConfigureAwait(false))
            return CriteriaEntryDecisionVerification.Pending;

        switch (evidence.Basis)
        {
            case "draft" when accepted && reason is null:
                if (!namedActiveDraft)
                    return CriteriaEntryDecisionVerification.Pending;
                if (terminal is null)
                    return CriteriaEntryDecisionVerification.Verified;
                if (named.AreaOffset >= terminal.AreaOffset || proposalRecord.AreaOffset >= terminal.AreaOffset)
                    return CriteriaEntryDecisionVerification.Pending;
                if (terminal.Event is CriteriaEditionDraftAbandoned abandoned)
                    return CriteriaEntryDecisionSourceRules.ValidAbandonment(abandoned)
                        ? CriteriaEntryDecisionVerification.Abandoned : CriteriaEntryDecisionVerification.Pending;
                return await PublishedAsync(intent.EditionId, terminal, ct).ConfigureAwait(false)
                    ? CriteriaEntryDecisionVerification.Verified : CriteriaEntryDecisionVerification.Pending;
            case "publication" when terminal is not null && terminal.Event is CriteriaEditionPublished &&
                                    terminal.Event.Metadata.EventId == evidence.EditionEventId:
                if (!(accepted && reason is null && proposalRecord.AreaOffset < terminal.AreaOffset ||
                      !accepted && reason == "edition_published" && proposalRecord.AreaOffset > terminal.AreaOffset))
                    return CriteriaEntryDecisionVerification.Pending;
                return await PublishedAsync(intent.EditionId, terminal, ct).ConfigureAwait(false)
                    ? CriteriaEntryDecisionVerification.Verified : CriteriaEntryDecisionVerification.Pending;
            case "abandonment" when !accepted && reason == "edition_abandoned" && terminal is not null &&
                                     terminal.Event is CriteriaEditionDraftAbandoned abandonmentFact &&
                                     CriteriaEntryDecisionSourceRules.ValidAbandonment(abandonmentFact) &&
                                     terminal.Event.Metadata.EventId == evidence.EditionEventId:
                return CriteriaEntryDecisionVerification.Abandoned;
            default:
                return CriteriaEntryDecisionVerification.Pending;
        }
    }

    async ValueTask<bool> PublishedAsync(Uuid editionId, CriteriaOrderedSourceEvent terminal, CancellationToken ct)
    {
        var proof = await publications.ReadPublishedAsync(editionId, ct).ConfigureAwait(false);
        return proof.IsSuccess && proof.Value is { } published && published.EditionId == editionId &&
               published.EventId == terminal.Event.Metadata.EventId && published.ResourceOffset == terminal.ResourceOffset;
    }

    static bool Exact(CriteriaOrderedSourceEvent item, EventStreamAddress stream, Uuid id) =>
        item.Stream == stream && item.Event.Metadata.AggregateId == id && !item.Event.Metadata.IsAudit &&
        item.Event.Metadata.EventId != Uuid.Empty && item.Event.Metadata.AggregateVersion == checked(item.ResourceOffset + 1);

    static bool Same(CriteriaOrderedSourceEvent? actual, CriteriaOrderedSourceEvent expected) =>
        actual is not null && actual.Stream == expected.Stream && actual.ResourceOffset == expected.ResourceOffset &&
        actual.AreaOffset == expected.AreaOffset && actual.Event == expected.Event &&
        actual.Event.Metadata.EventId == expected.Event.Metadata.EventId &&
        actual.Event.Metadata.AggregateId == expected.Event.Metadata.AggregateId &&
        actual.Event.Metadata.AggregateVersion == expected.Event.Metadata.AggregateVersion && !actual.Event.Metadata.IsAudit;
}
