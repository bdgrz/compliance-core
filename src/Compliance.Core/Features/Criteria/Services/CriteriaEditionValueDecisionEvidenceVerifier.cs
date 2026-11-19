using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Independently revalidates one committed value outcome through its actual source position.</summary>
sealed class CriteriaEditionValueDecisionEvidenceVerifier(ICriteriaExactSourceReader source,
    ICriteriaSourceCutCoverage coverage, ICriteriaPublicationAuthority publications, ICriteriaFreshEntryHead freshHead,
    ICriteriaParticipantCausalOrder? causalOrder = null)
{
    public async ValueTask<CriteriaEntryDecisionVerification> VerifyAsync(CriteriaOrderedSourceEvent proposalRecord,
        CriteriaOrderedSourceEvent decisionRecord, CancellationToken ct)
    {
        if (proposalRecord.Event is not CriteriaEditionValueProposed proposal || !CriteriaEditionValueRules.Shape(proposal.Intent) ||
            !CriteriaEditionValueRules.Admission(proposal.Intent, proposal.ExecutionGeneration, proposal.Actor, proposal.Admission))
            return CriteriaEntryDecisionVerification.Pending;
        var i = proposal.Intent;
        var owner = new CriteriaEditionValue(i.EditionId, i.TargetKind, i.Field, i.Category, i.Code);
        if (!Exact(proposalRecord, owner.Stream, owner.Id) || !Exact(decisionRecord, owner.Stream, owner.Id) ||
            proposalRecord.AreaOffset >= decisionRecord.AreaOffset || proposalRecord.ResourceOffset >= decisionRecord.ResourceOffset)
            return CriteriaEntryDecisionVerification.Pending;
        var (accepted, reason, evidence, editionId, operation, generation, proposalId) = decisionRecord.Event switch
        {
            CriteriaEditionValueAccepted value => (true, (string?)null, value.Evidence, value.EditionId,
                value.OperationId, value.ExecutionGeneration, value.ProposalEventId),
            CriteriaEditionValueRefused value => (false, (string?)value.Reason, value.Evidence, value.EditionId,
                value.OperationId, value.ExecutionGeneration, value.ProposalEventId),
            _ => (false, null, null, Uuid.Empty, Uuid.Empty, 0L, Uuid.Empty),
        };
        if (evidence is null || evidence.EditionEventId == Uuid.Empty || editionId != i.EditionId || operation != i.OperationId ||
            generation != proposal.ExecutionGeneration || proposalId != proposal.Metadata.EventId ||
            evidence.ProposalAreaOffset != proposalRecord.AreaOffset || evidence.EditionAreaOffset >= decisionRecord.AreaOffset ||
            evidence.EditionAreaOffset == proposalRecord.AreaOffset)
            return CriteriaEntryDecisionVerification.Pending;
        var actualProposal = await source.FindAsync(owner.Stream, owner.Id, proposal.Metadata.EventId, ct).ConfigureAwait(false);
        var actualDecision = await source.FindAsync(owner.Stream, owner.Id, decisionRecord.Event.Metadata.EventId, ct).ConfigureAwait(false);
        if (!Same(actualProposal, proposalRecord) || !Same(actualDecision, decisionRecord))
            return CriteriaEntryDecisionVerification.Pending;

        var claimOwner = new CriteriaEditionValueOperation(i.EditionId, i.TargetKind, i.Field, i.Category, i.Code, i.OperationId);
        var claimRecord = await source.FindAsync(claimOwner.Stream, claimOwner.Id, proposal.OperationClaimEventId, ct).ConfigureAwait(false);
        if (claimRecord?.Event is not CriteriaEditionValueOperationClaimed claim || !Exact(claimRecord, claimOwner.Stream, claimOwner.Id) ||
            claim.Metadata.EventId != proposal.OperationClaimEventId || claim.Intent != i || claim.Actor != proposal.Actor ||
            claim.Admission is null || claim.Admission.UserId != proposal.Admission.UserId ||
            claim.ExecutionGeneration != proposal.ExecutionGeneration ||
            !CriteriaEditionValueRules.Admission(claim.Intent, claim.ExecutionGeneration, claim.Actor, claim.Admission))
            return CriteriaEntryDecisionVerification.Pending;

        var edition = new CriteriaEdition(i.EditionId);
        CriteriaOrderedSourceEvent? request = null, drafted = null, named = null, terminal = null;
        var lifecycle = new CriteriaEntryEditionLifecycle(i.EditionId, i.DraftGeneration);
        var participants = new CriteriaValueEditionParticipantVerifier(source, coverage, causalOrder);
        var namedActiveDraft = false;
        ulong next = 0;
        await foreach (var item in source.ReadAsync(edition.Stream, edition.Id, ct).ConfigureAwait(false))
        {
            if (!Exact(item, edition.Stream, edition.Id) || item.ResourceOffset != next++)
                throw new InvalidOperationException("The edition source is not a contiguous exact history.");
            if (!lifecycle.Apply(item.Event))
                return CriteriaEntryDecisionVerification.Pending;
            if (!await participants.ObserveAsync(item, ct).ConfigureAwait(false))
                return CriteriaEntryDecisionVerification.Pending;
            if (item.Event is CriteriaEditionDraftRequested r && r.Metadata.EventId == proposal.EditionRequestEventId &&
                r.EditionId == i.EditionId && r.DraftGeneration == i.DraftGeneration && r.OperationId == i.DraftGeneration &&
                r.Framework == "tsc" && r.SeedVersion is null && r.SeedFingerprint is null)
                request = item;
            if (item.Event is CriteriaEditionDrafted d && d.EditionId == i.EditionId && d.DraftGeneration == i.DraftGeneration && d.Framework == "tsc")
                drafted = item;
            if (item.Event.Metadata.EventId == evidence.EditionEventId)
            {
                named = item;
                namedActiveDraft = lifecycle.ValueActive;
            }
            if (item.AreaOffset >= decisionRecord.AreaOffset)
                continue;
            if (item.Event is CriteriaEditionPublished published && published.EditionId == i.EditionId && published.DraftGeneration == i.DraftGeneration ||
                item.Event is CriteriaEditionDraftAbandoned abandoned && abandoned.EditionId == i.EditionId && abandoned.DraftGeneration == i.DraftGeneration)
            {
                if (terminal is not null)
                    throw new InvalidOperationException("The edition has multiple terminal facts.");
                terminal = item;
            }
        }
        if (request is null || drafted is null || named is null ||
            !(request.AreaOffset < drafted.AreaOffset && drafted.AreaOffset < claimRecord.AreaOffset &&
              claimRecord.AreaOffset < proposalRecord.AreaOffset) ||
            named.ResourceOffset != evidence.EditionResourceOffset || named.AreaOffset != evidence.EditionAreaOffset ||
            named.ResourceOffset < drafted.ResourceOffset ||
            !await new CriteriaEditionValueDecisionAuthority(source, coverage, publications, freshHead, causalOrder)
                .VerifyDraftBindingAsync(request, drafted, ct).ConfigureAwait(false) ||
            !await coverage.CoversAsync(edition.Stream, next, decisionRecord.AreaOffset, ct).ConfigureAwait(false))
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
                return await PublishedAsync(i.EditionId, terminal, ct).ConfigureAwait(false)
                    ? CriteriaEntryDecisionVerification.Verified : CriteriaEntryDecisionVerification.Pending;
            case "publication" when terminal?.Event is CriteriaEditionPublished && terminal.Event.Metadata.EventId == evidence.EditionEventId:
                if (!(accepted && reason is null && proposalRecord.AreaOffset < terminal.AreaOffset ||
                      !accepted && reason == "edition_published" && proposalRecord.AreaOffset > terminal.AreaOffset))
                    return CriteriaEntryDecisionVerification.Pending;
                return await PublishedAsync(i.EditionId, terminal, ct).ConfigureAwait(false)
                    ? CriteriaEntryDecisionVerification.Verified : CriteriaEntryDecisionVerification.Pending;
            case "abandonment" when !accepted && reason == "edition_abandoned" &&
                terminal?.Event is CriteriaEditionDraftAbandoned abandonment && CriteriaEntryDecisionSourceRules.ValidAbandonment(abandonment) &&
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
