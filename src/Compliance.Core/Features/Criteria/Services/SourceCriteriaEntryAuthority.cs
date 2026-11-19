using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Resolves one entry and, when needed, its one exact parent at the publication cut.</summary>
/// <remarks>Absence and the latest effective state require independently qualified source coverage.</remarks>
public sealed class SourceCriteriaEntryAuthority(
    ICriteriaExactSourceReader source,
    ICriteriaPublicationAuthority publications,
    ICriteriaSourceCutCoverage coverage, ICriteriaParticipantCausalOrder? causal = null) : ICriteriaEntryAuthority
{
    static readonly CriteriaEntryAuthorityResult Pending = new(CriteriaEntryAuthorityStatus.Pending, null);
    static readonly CriteriaEntryAuthorityResult NotMember = new(CriteriaEntryAuthorityStatus.NotMember, null);

    /// <inheritdoc />
    public async ValueTask<Result<CriteriaEntryAuthorityResult>> ReadAsync(Uuid editionId, string identifier,
        CancellationToken ct = default)
    {
        if (editionId == Uuid.Empty || !CriteriaContentBounds.Identifier(identifier))
            return Result<CriteriaEntryAuthorityResult>.Failure(new RequestError(RequestErrorKind.Validation,
                "An edition and criterion identifier are required."));

        var publication = await publications.ReadPublishedAsync(editionId, ct).ConfigureAwait(false);
        if (!publication.IsSuccess)
            return Result<CriteriaEntryAuthorityResult>.Failure(publication.Error!);
        var published = publication.Value;
        if (published is null || published.EditionId != editionId || published.EventId == Uuid.Empty)
            throw new InvalidOperationException("Publication authority returned a mismatched edition.");

        var edition = new CriteriaEdition(editionId);
        var cut = await source.FindAsync(edition.Stream, edition.Id, published.EventId, ct).ConfigureAwait(false);
        if (cut is null)
            return Result<CriteriaEntryAuthorityResult>.Success(Pending);
        if (cut.ResourceOffset != published.ResourceOffset || cut.Event is not CriteriaEditionPublished marker ||
            marker.EditionId != editionId || marker.Metadata.EventId != published.EventId)
            throw new InvalidOperationException("The committed publication does not match its source proof.");

        var current = await ReadAtCutAsync(editionId, identifier, marker.DraftGeneration, cut.AreaOffset, ct)
            .ConfigureAwait(false);
        if (!current.Complete || current.Pending)
            return Result<CriteriaEntryAuthorityResult>.Success(Pending);
        if (current.Proposal is not { } entry || current.Decision is not { } decision)
            return Result<CriteriaEntryAuthorityResult>.Success(NotMember);

        SourceProposal? parent = null;
        CriterionChanged? parentDecision = null;
        if (entry.Intent.Kind == "point_of_focus")
        {
            if (entry.Intent.ParentIdentifier is not { Length: > 0 } parentId ||
                entry.Intent.ParentIncarnationId is not { } parentIncarnation || parentIncarnation == Uuid.Empty ||
                entry.ParentAcceptedEventId is not { } parentAccepted || parentAccepted == Uuid.Empty)
                throw new InvalidOperationException("The point of focus has no bound parent acceptance.");
            var resolved = await ReadAtCutAsync(editionId, parentId, marker.DraftGeneration, cut.AreaOffset, ct)
                .ConfigureAwait(false);
            if (!resolved.Complete || resolved.Pending)
                return Result<CriteriaEntryAuthorityResult>.Success(Pending);
            parent = resolved.Proposal;
            parentDecision = resolved.Decision;
            if (parent is null || parentDecision is null || parent.IncarnationId != parentIncarnation ||
                parent.Intent.Kind != "criterion" || parent.Intent.Category != entry.Intent.Category)
                return Result<CriteriaEntryAuthorityResult>.Success(NotMember);
            if (parentDecision.Metadata.EventId != parentAccepted || resolved.AcceptedAreaOffset is not { } parentArea ||
                parentArea >= entry.AreaOffset)
                throw new InvalidOperationException("The point of focus's parent evidence does not match the accepted incarnation.");
        }
        else if (entry.Intent.Kind != "criterion")
        {
            throw new InvalidOperationException("The accepted entry has an unknown kind.");
        }

        var intent = entry.Intent;
        if (intent.Category is null || current.Summary is null)
            throw new InvalidOperationException("The accepted entry lacks its category or summary.");
        var item = new Criterion(editionId, identifier, intent.Kind == "criterion" ? identifier : null,
            intent.Category, intent.Kind!, intent.ParentIdentifier, current.Summary);
        var proof = new CriteriaEntryProof(item, entry.IncarnationId, published.EventId,
            entry.EventId, decision.Metadata.EventId, entry.Intent.ParentIncarnationId,
            parent?.EventId, parentDecision?.Metadata.EventId);
        return Result<CriteriaEntryAuthorityResult>.Success(new CriteriaEntryAuthorityResult(
            CriteriaEntryAuthorityStatus.Accepted, proof));
    }

    async ValueTask<EntryAtCut> ReadAtCutAsync(Uuid editionId, string identifier, Uuid generation,
        ulong cutAreaOffset, CancellationToken ct)
    {
        var owner = new CriteriaEntry(editionId, identifier);
        SourceProposal? active = null;
        SourceProposal? unresolved = null;
        ulong? acceptedArea = null;
        CriterionChanged? accepted = null;
        string? summary = null;
        ulong next = 0;
        await foreach (var record in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            next = checked(record.ResourceOffset + 1);
            switch (record.Event)
            {
                case CriterionChangeProposed proposed when record.AreaOffset < cutAreaOffset:
                    if (unresolved is not null || proposed.Intent.EditionId != editionId ||
                        proposed.Intent.Identifier != identifier || proposed.Intent.DraftGeneration != generation)
                        throw new InvalidOperationException("The entry's ordered proposals are inconsistent.");
                    if (!await new CriteriaHumanBindingVerifier(source).VerifyProposalAsync(proposed,
                            record.AreaOffset, ct).ConfigureAwait(false))
                        return new EntryAtCut(false, true, null, null, null, null);
                    unresolved = new SourceProposal(proposed.Intent, proposed.ExecutionGeneration, proposed.IncarnationId,
                        proposed.ParentAcceptedEventId, proposed.Metadata.EventId, record.AreaOffset, record);
                    break;
                case CriterionSeedProposed seed when record.AreaOffset < cutAreaOffset:
                    if (unresolved is not null || seed.Intent.EditionId != editionId ||
                        seed.Intent.Identifier != identifier || seed.Intent.DraftGeneration != generation)
                        throw new InvalidOperationException("The seed entry ordered proposals are inconsistent.");
                    if (!await new CriteriaSeedBindingVerifier(source).VerifyProposalAsync(seed, record.AreaOffset, ct)
                        .ConfigureAwait(false))
                        return new EntryAtCut(false, true, null, null, null, null);
                    unresolved = new SourceProposal(seed.Intent, seed.ExecutionGeneration, seed.IncarnationId,
                        seed.ParentAcceptedEventId, seed.Metadata.EventId, record.AreaOffset, record);
                    break;
                case CriterionChanged changed when unresolved is not null && Matches(unresolved, changed):
                    if (await new CriteriaEntryDecisionEvidenceVerifier(source, coverage, publications, causal)
                            .VerifyAsync(unresolved.Record, record, ct).ConfigureAwait(false) !=
                        CriteriaEntryDecisionVerification.Verified)
                        return new EntryAtCut(false, true, null, null, null, null);
                    if (unresolved.Intent.Kind == "point_of_focus" && unresolved.Intent.Action != "remove" &&
                        !await CriteriaEntryDecisionAuthority.ParentActiveAsync(source, coverage, publications,
                            unresolved.Intent, unresolved.ParentAcceptedEventId, unresolved.AreaOffset, ct, causal).ConfigureAwait(false))
                        return new EntryAtCut(false, true, null, null, null, null);
                    if (unresolved.Intent.Action == "remove")
                    {
                        active = null;
                        accepted = null;
                        acceptedArea = null;
                        summary = null;
                    }
                    else if (unresolved.Intent.Action is "create" or "replace")
                    {
                        active = unresolved;
                        accepted = changed;
                        acceptedArea = record.AreaOffset;
                        summary = unresolved.Intent.Summary;
                    }
                    else
                    {
                        throw new InvalidOperationException("The entry has an unknown accepted action.");
                    }
                    unresolved = null;
                    break;
                case CriterionChangeRefused refused when unresolved is not null && Matches(unresolved, refused):
                    if (await new CriteriaEntryDecisionEvidenceVerifier(source, coverage, publications, causal)
                            .VerifyAsync(unresolved.Record, record, ct).ConfigureAwait(false) !=
                        CriteriaEntryDecisionVerification.Verified)
                        return new EntryAtCut(false, true, null, null, null, null);
                    unresolved = null;
                    break;
                case CriterionSummaryCorrected corrected when record.AreaOffset < cutAreaOffset && active is not null:
                    if (corrected.Intent.EditionId != editionId || corrected.Intent.Identifier != identifier)
                        throw new InvalidOperationException("The summary correction addresses another entry.");
                    if (!await new CriteriaHumanBindingVerifier(source).VerifySummaryAsync(corrected,
                            record.AreaOffset, ct).ConfigureAwait(false))
                        return new EntryAtCut(false, true, null, null, null, null);
                    summary = corrected.Intent.Summary;
                    break;
            }
        }

        var complete = await coverage.CoversAsync(owner.Stream, next, cutAreaOffset, ct).ConfigureAwait(false);
        return new EntryAtCut(complete, unresolved is not null, active, accepted, summary, acceptedArea);
    }

    static bool Matches(SourceProposal proposal, CriterionChanged outcome) =>
        outcome.EditionId == proposal.Intent.EditionId && outcome.Identifier == proposal.Intent.Identifier &&
        outcome.OperationId == proposal.Intent.OperationId && outcome.ExecutionGeneration == proposal.ExecutionGeneration &&
        outcome.ProposalEventId == proposal.EventId;

    static bool Matches(SourceProposal proposal, CriterionChangeRefused outcome) =>
        outcome.EditionId == proposal.Intent.EditionId && outcome.Identifier == proposal.Intent.Identifier &&
        outcome.OperationId == proposal.Intent.OperationId && outcome.ExecutionGeneration == proposal.ExecutionGeneration &&
        outcome.ProposalEventId == proposal.EventId;

    sealed record SourceProposal(CriterionChangeIntent Intent, long ExecutionGeneration, Uuid IncarnationId,
        Uuid? ParentAcceptedEventId, Uuid EventId, ulong AreaOffset, CriteriaOrderedSourceEvent Record);

    sealed record EntryAtCut(bool Complete, bool Pending, SourceProposal? Proposal,
        CriterionChanged? Decision, string? Summary, ulong? AcceptedAreaOffset);
}
