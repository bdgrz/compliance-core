using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Services;

/// <summary>Checks runtime coverage discovery against the edition's committed publication prefix.</summary>
/// <remarks>
/// Streams the existing ordered area without retaining its children. Each pre-cut entry
/// proposal must have a settled source outcome and a matching runtime projection result.
/// The caller separately validates every returned page item and the page's publication ID.
/// </remarks>
sealed class RuntimeCriteriaCoverageSource(IDomainEventReader events, IRuntimeCriteriaCatalogReader runtime,
    ICriteriaEntryAuthority entries, ICriteriaPublicationAuthority publications)
{
    internal async ValueTask<bool> CoversAsync(Uuid editionId, CriteriaPublicationProof expected, CancellationToken ct)
    {
        var result = await publications.ReadPublishedAsync(editionId, ct).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value != expected || expected.EditionId != editionId || expected.EventId == Uuid.Empty)
            return false;
        var edition = new CriteriaEdition(editionId);
        var pattern = EventStreamPattern.ForPattern(edition.Stream.Realm, edition.Stream.Area);
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Stream.Realm != edition.Stream.Realm || record.Stream.Area != edition.Stream.Area)
                return false;
            if (record.Stream == edition.Stream && record.Event.Metadata.EventId == expected.EventId)
                return record.ResourceOffset == expected.ResourceOffset && record.Event is CriteriaEditionPublished published &&
                    !published.Metadata.IsAudit && published.Metadata.AggregateId == edition.Id && published.EditionId == editionId;
            var intent = record.Event switch
            {
                CriterionChangeProposed human => human.Intent,
                CriterionSeedProposed seed => seed.Intent,
                _ => null,
            };
            if (intent is null)
                continue;
            if (intent.EditionId != editionId || string.IsNullOrWhiteSpace(intent.Identifier))
                return false;
            var owner = new CriteriaEntry(editionId, intent.Identifier);
            if (record.Stream != owner.Stream || record.Event.Metadata.AggregateId != owner.Id || record.Event.Metadata.IsAudit)
                return false;
            var authoritative = await entries.ReadAsync(editionId, intent.Identifier, ct).ConfigureAwait(false);
            if (!authoritative.IsSuccess || authoritative.Value is null)
                return false;
            var projected = await runtime.GetEntryAsync(editionId, intent.Identifier, ct).ConfigureAwait(false);
            if (projected.IsSuccess && (projected.Value is null || projected.Value.EditionId != editionId ||
                projected.Value.Identifier != intent.Identifier))
                return false;
            if (authoritative.Value.Status == CriteriaEntryAuthorityStatus.NotMember && authoritative.Value.Proof is null)
            {
                if (!projected.IsSuccess && projected.Error!.Kind == RequestErrorKind.NotFound)
                    continue;
                if (projected.IsSuccess && projected.Value is { Status: "ineffective" })
                    continue;
                return false;
            }
            if (!authoritative.IsSuccess || authoritative.Value.Status != CriteriaEntryAuthorityStatus.Accepted ||
                authoritative.Value.Proof is not { } proof || !projected.IsSuccess ||
                !Matches(projected.Value, proof, expected.EventId))
                return false;
        }
        return false;
    }

    internal static bool Matches(RuntimeCriteriaEntry? row, CriteriaEntryProof proof, Uuid publicationId) =>
        row is { Status: "accepted" } && proof.Entry is not null && proof.PublicationEventId == publicationId && publicationId != Uuid.Empty &&
        proof.IncarnationId != Uuid.Empty && proof.ProposalEventId != Uuid.Empty && proof.DecisionEventId != Uuid.Empty &&
        (proof.Entry.Kind == "criterion" && proof.Entry.ParentIdentifier is null && proof.ParentIncarnationId is null &&
             proof.ParentProposalEventId is null && proof.ParentDecisionEventId is null ||
         proof.Entry.Kind == "point_of_focus" && !string.IsNullOrWhiteSpace(proof.Entry.ParentIdentifier) &&
             proof.ParentIncarnationId is { } parent && parent != Uuid.Empty &&
             proof.ParentProposalEventId is { } parentProposal && parentProposal != Uuid.Empty &&
             proof.ParentDecisionEventId is { } parentDecision && parentDecision != Uuid.Empty) &&
        row.EditionId == proof.Entry.EditionId && row.Identifier == proof.Entry.Identifier &&
        row.IncarnationId == proof.IncarnationId && row.Kind == proof.Entry.Kind && row.Category == proof.Entry.Category &&
        row.ParentIdentifier == proof.Entry.ParentIdentifier && row.ParentIncarnationId == proof.ParentIncarnationId;
}
