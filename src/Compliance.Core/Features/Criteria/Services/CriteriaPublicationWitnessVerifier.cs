using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Proves one criterion incarnation was accepted and active at the witness pin.</summary>
sealed class CriteriaPublicationWitnessVerifier(ICriteriaExactSourceReader source, ICriteriaSourceCutCoverage coverage,
    ICriteriaParticipantCausalOrder? causal = null)
{
    internal async ValueTask<bool> VerifyAsync(Uuid editionId, string identifier, Uuid draftGeneration,
        Uuid incarnationId, ulong? pinArea, ulong? expectedHead, CancellationToken ct)
    {
        var owner = new CriteriaEntry(editionId, identifier);
        CriteriaOrderedSourceEvent? pending = null;
        CriterionChangeIntent? active = null;
        var activeIncarnation = Uuid.Empty;
        ulong next = 0;
        await foreach (var item in source.ReadAsync(owner.Stream, owner.Id, ct).ConfigureAwait(false))
        {
            if (item.Stream != owner.Stream || item.ResourceOffset != next++ ||
                item.Event.Metadata.AggregateId != owner.Id || item.Event.Metadata.EventId == Uuid.Empty ||
                item.Event.Metadata.AggregateVersion != item.ResourceOffset + 1 || item.Event.Metadata.IsAudit)
                return false;
            if (pinArea is { } cut && item.AreaOffset >= cut)
                continue;
            switch (item.Event)
            {
                case CriterionChangeProposed human:
                    if (pending is not null || human.Intent.EditionId != editionId || human.Intent.Identifier != identifier ||
                        human.Intent.DraftGeneration != draftGeneration ||
                        !await new CriteriaHumanBindingVerifier(source).VerifyProposalAsync(human, item.AreaOffset, ct).ConfigureAwait(false))
                        return false;
                    pending = item;
                    break;
                case CriterionSeedProposed seed:
                    if (pending is not null || seed.Intent.EditionId != editionId || seed.Intent.Identifier != identifier ||
                        seed.Intent.DraftGeneration != draftGeneration ||
                        !await new CriteriaSeedBindingVerifier(source).VerifyProposalAsync(seed, item.AreaOffset, ct).ConfigureAwait(false))
                        return false;
                    pending = item;
                    break;
                case CriterionChanged or CriterionChangeRefused:
                    if (pending is null || await new CriteriaEntryDecisionEvidenceVerifier(source, coverage, NoPublication.Instance, causal)
                            .VerifyAsync(pending, item, ct).ConfigureAwait(false) != CriteriaEntryDecisionVerification.Verified)
                        return false;
                    if (item.Event is CriterionChanged)
                    {
                        var (intent, incarnation) = pending.Event switch
                        {
                            CriterionChangeProposed p => (p.Intent, p.IncarnationId),
                            CriterionSeedProposed p => (p.Intent, p.IncarnationId),
                            _ => throw new InvalidOperationException("The witness proposal has an unknown type."),
                        };
                        if (intent.Action == "remove")
                        { active = null; activeIncarnation = Uuid.Empty; }
                        else if (intent.Action is "create" or "replace")
                        { active = intent; activeIncarnation = incarnation; }
                        else
                            return false;
                    }
                    pending = null;
                    break;
            }
        }
        if (expectedHead is { } head && next != head)
            return false;
        if (pinArea is { } pin && !await coverage.CoversAsync(owner.Stream, next, pin, ct).ConfigureAwait(false))
            return false;
        return pending is null && active is { Kind: "criterion" } && activeIncarnation == incarnationId;
    }

    // A witness decision must precede its pin, and the pin precedes publication. It cannot
    // rely on the publication proof currently being established.
    sealed class NoPublication : ICriteriaPublicationAuthority
    {
        internal static NoPublication Instance { get; } = new();
        public ValueTask<Result<CriteriaPublicationProof>> ReadPublishedAsync(Uuid editionId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<CriteriaPublicationProof>.Failure(
                new RequestError(RequestErrorKind.Conflict, "Publication proof is not available here.", true)));
    }
}
