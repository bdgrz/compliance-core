using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Verifies an observed immutable seed binding at its exact source address.</summary>
/// <remarks>
/// This positive fact does not certify a current draft or authorize a write.
/// A missing observation remains pending, and callers must separately establish
/// system identity, source currentness and any parent or publication authority.
/// </remarks>
sealed class CriteriaSeedBindingVerifier(ICriteriaExactSourceReader source)
{
    public async ValueTask<bool> VerifyProposalAsync(CriterionSeedProposed proposal, ulong proposalAreaOffset,
        CancellationToken ct)
    {
        if (!PlatformCriteriaSeedDefinition.MatchesProposal(proposal))
            throw new InvalidOperationException("The seed proposal differs from the compiled release.");
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var header = new CriteriaSeedHeader(edition.EditionId, edition.Framework, edition.EditionLabel,
            edition.PublishedAt, edition.IsComplete, edition.CoverageNote, edition.SourceUrl, edition.ContentRights,
            proposal.SeedVersion, proposal.SeedFingerprint);
        var owner = new CriteriaEdition(edition.EditionId);
        var binding = await source.FindAsync(owner.Stream, owner.Id, proposal.EditionRequestEventId, ct).ConfigureAwait(false);
        if (binding is null)
            return false;
        if (!Matches(header, proposal.EditionRequestEventId, binding, owner))
            throw new InvalidOperationException("The seed proposal refers to a different edition request.");
        var intent = proposal.Intent;
        var claimAddress = CriteriaEntryOperation.AddressOf(intent.EditionId, intent.Identifier, intent.OperationId);
        var claimId = CriteriaEntryOperation.IdentityOf(intent.EditionId, intent.Identifier, intent.OperationId);
        var claim = await source.FindAsync(claimAddress, claimId, proposal.OperationClaimEventId, ct).ConfigureAwait(false);
        if (claim is null)
            return false;
        if (claim.Stream != claimAddress || claim.Event.Metadata.AggregateId != claimId ||
            claim.Event.Metadata.EventId != proposal.OperationClaimEventId || claim.Event.Metadata.IsAudit ||
            claim.Event is not CriterionEntryOperationSeedClaimed recorded || recorded.Intent != intent ||
            recorded.IncarnationId != proposal.IncarnationId ||
            recorded.ParentAcceptedEventId != proposal.ParentAcceptedEventId ||
            recorded.EditionRequestEventId != proposal.EditionRequestEventId || recorded.SeedVersion != proposal.SeedVersion ||
            recorded.SeedFingerprint != proposal.SeedFingerprint)
            throw new InvalidOperationException("The seed operation claim does not match its proposal.");
        // A later execution may be legitimate after an exact target fence. That
        // recovery proof is not implemented here, so leave it unresolved.
        if (recorded.ExecutionGeneration != proposal.ExecutionGeneration)
            return false;
        if (binding.AreaOffset >= claim.AreaOffset || claim.AreaOffset >= proposalAreaOffset)
            throw new InvalidOperationException("The seed binding and operation claim must precede the entry proposal.");
        return true;
    }

    public async ValueTask<CriteriaSeedWriteResult> VerifyAsync(CriteriaSeedHeader header, Uuid eventId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(header);
        if (!PlatformCriteriaSeedDefinition.MatchesHeader(header) || eventId == Uuid.Empty)
            return new CriteriaSeedWriteResult(CriteriaSeedWriteStatus.Conflict, Uuid.Empty);

        var owner = new CriteriaEdition(header.EditionId);
        var sourceEvent = await source.FindAsync(owner.Stream, owner.Id, eventId, ct).ConfigureAwait(false);
        if (sourceEvent is null)
            return new CriteriaSeedWriteResult(CriteriaSeedWriteStatus.Pending, Uuid.Empty);
        return Matches(header, eventId, sourceEvent, owner)
            ? new CriteriaSeedWriteResult(CriteriaSeedWriteStatus.Confirmed, eventId)
            : new CriteriaSeedWriteResult(CriteriaSeedWriteStatus.Conflict, Uuid.Empty);
    }

    static bool Matches(CriteriaSeedHeader header, Uuid eventId, CriteriaOrderedSourceEvent sourceEvent, CriteriaEdition owner)
    {
        var operation = PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, "edition");
        return sourceEvent.Stream == owner.Stream && sourceEvent.Event.Metadata.AggregateId == owner.Id &&
            sourceEvent.Event.Metadata.EventId == eventId && !sourceEvent.Event.Metadata.IsAudit &&
            sourceEvent.Event.Metadata.AggregateVersion == checked(sourceEvent.ResourceOffset + 1) &&
            sourceEvent.Event is CriteriaEditionDraftRequested requested &&
            requested.EditionId == header.EditionId && requested.DraftGeneration == operation &&
            requested.OperationId == operation && requested.ExecutionGeneration > 0 &&
            requested.Framework == header.Framework && requested.Label == header.EditionLabel && requested.SeedVersion == header.SeedVersion &&
            requested.SeedFingerprint == header.SeedFingerprint && requested.Admission is null &&
            requested.Actor == new ActorAttribution("criteria-seed-bootstrap", "system");
    }
}
