using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Finds a point of focus's exact accepted parent in that parent's own stream.</summary>
/// <remarks>
/// This proves that a criterion incarnation was accepted, not that it is still
/// effective at a later publication cut. An unobserved acceptance is pending and
/// cannot be treated as either absence or a refusal.
/// </remarks>
public sealed class CriteriaParentAcceptanceReader(ICriteriaExactSourceReader events,
    ICriteriaSourceCutCoverage? coverage = null, ICriteriaPublicationAuthority? publications = null,
    ICriteriaParticipantCausalOrder? causal = null)
{
    /// <summary>Reads the accepted exact parent identity and category from source.</summary>
    public async ValueTask<Result<CriteriaParentAcceptance>> ReadAsync(Uuid editionId, string identifier,
        Uuid incarnationId, string category, CancellationToken ct = default)
    {
        if (editionId == Uuid.Empty || incarnationId == Uuid.Empty || !CriteriaContentBounds.Identifier(identifier) ||
            string.IsNullOrWhiteSpace(category))
            return Result<CriteriaParentAcceptance>.Failure(new RequestError(RequestErrorKind.Validation,
                "An edition, parent identifier, incarnation and category are required."));

        var parent = new CriteriaEntry(editionId, identifier);
        var proposalEventId = Uuid.Empty;
        var proposalOperationId = Uuid.Empty;
        long proposalGeneration = 0;
        CriteriaOrderedSourceEvent? proposalRecord = null;
        ulong position = 0;
        await foreach (var record in events.ReadAsync(parent.Stream, parent.Id, ct).ConfigureAwait(false))
        {
            if (record.Stream != parent.Stream || record.ResourceOffset != position ||
                record.Event.Metadata.AggregateId != parent.Id || record.Event.Metadata.IsAudit)
                throw new InvalidOperationException("The parent criterion source is not a contiguous entry history.");
            position = checked(position + 1);

            if (record.Event is CriterionChangeProposed candidate && candidate.IncarnationId == incarnationId &&
                candidate.Intent.EditionId == editionId && candidate.Intent.Identifier == identifier &&
                candidate.Intent.Kind == "criterion" && candidate.Intent.Category == category &&
                candidate.Intent.Action is "create" or "replace")
            {
                if (!await new CriteriaHumanBindingVerifier(events).VerifyProposalAsync(candidate, record.AreaOffset, ct)
                    .ConfigureAwait(false))
                    return Result<CriteriaParentAcceptance>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The parent operation claim has not been qualified.", true));
                proposalEventId = candidate.Metadata.EventId;
                proposalOperationId = candidate.Intent.OperationId;
                proposalGeneration = candidate.ExecutionGeneration;
                proposalRecord = record;
                continue;
            }
            if (record.Event is CriterionSeedProposed seed && seed.IncarnationId == incarnationId &&
                seed.Intent.EditionId == editionId && seed.Intent.Identifier == identifier &&
                seed.Intent.Kind == "criterion" && seed.Intent.Category == category)
            {
                if (!await new CriteriaSeedBindingVerifier(events).VerifyProposalAsync(seed, record.AreaOffset, ct)
                    .ConfigureAwait(false))
                    return Result<CriteriaParentAcceptance>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The seed edition request has not been observed; retry when its source is available.", true));
                proposalEventId = seed.Metadata.EventId;
                proposalOperationId = seed.Intent.OperationId;
                proposalGeneration = seed.ExecutionGeneration;
                proposalRecord = record;
                continue;
            }

            if (proposalEventId == Uuid.Empty || record.Event is not CriterionChanged accepted ||
                accepted.EditionId != editionId || accepted.Identifier != identifier ||
                accepted.OperationId != proposalOperationId ||
                accepted.ExecutionGeneration != proposalGeneration ||
                accepted.ProposalEventId != proposalEventId)
                continue;

            if (accepted.Metadata.EventId == Uuid.Empty)
                throw new InvalidOperationException("The parent acceptance is missing its durable source identity.");
            if (proposalRecord is null || coverage is null || publications is null ||
                await new CriteriaEntryDecisionEvidenceVerifier(events, coverage, publications, causal)
                    .VerifyAsync(proposalRecord, record, ct).ConfigureAwait(false) !=
                CriteriaEntryDecisionVerification.Verified)
                return Result<CriteriaParentAcceptance>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The parent acceptance source evidence has not been qualified.", true));
            return Result<CriteriaParentAcceptance>.Success(new CriteriaParentAcceptance(editionId, identifier,
                incarnationId, category, proposalEventId, accepted.Metadata.EventId));
        }

        return Result<CriteriaParentAcceptance>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The exact parent acceptance has not been observed; retry when its outcome is available.", true));
    }
}
