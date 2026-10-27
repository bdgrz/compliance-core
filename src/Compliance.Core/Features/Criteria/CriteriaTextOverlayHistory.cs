using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Verified licensed source history outside the bounded overlay command owner; public output still uses its usage policy.</summary>
public static class CriteriaTextOverlayHistory
{
    /// <summary>Composes the exact supplied revisions at the hydrated owner's immutable source cut.</summary>
    public static async Task<IReadOnlyList<CriteriaTextOverlayRevision>> ReadAsync(IAggregateReader reader,
        IDomainEventReader events, IImmutableContentStore content, Uuid tenant, Uuid edition, string identifier,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new CriteriaTextOverlay(tenant, edition, identifier), ct).ConfigureAwait(false);
        var facts = await NativeOwnerHistory.ReadAsync<CriteriaTextOverlayEntryRevised>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 &&
            (owner.Latest is not { } latest || ContentDigest.OfJson(facts[^1]) != ContentDigest.OfJson(latest)))
            throw new ImmutableContentIntegrityException("The supplied overlay history differs from its hydrated native cut.");
        var revisions = new List<CriteriaTextOverlayRevision>(facts.Count);
        foreach (var fact in facts)
        {
            if (fact.TenantId != tenant || fact.EditionId != edition || fact.Identifier != identifier ||
                fact.Revision != revisions.Count + 1)
                throw new ImmutableContentIntegrityException("The supplied overlay source has another criterion or revision.");
            revisions.Add(await ReadRevisionAsync(content, fact, ct).ConfigureAwait(false));
        }
        return revisions;
    }

    /// <summary>Verifies one original licensed source body without changing its text, flags, attribution or accepted time.</summary>
    public static async Task<CriteriaTextOverlayRevision> ReadRevisionAsync(IImmutableContentStore content,
        CriteriaTextOverlayEntryRevised fact, CancellationToken ct = default)
    {
        var owner = new CriteriaTextOverlay(fact.TenantId, fact.EditionId, fact.Identifier);
        if (fact.OverlayContent.Schema != CriteriaTextOverlay.ContentSchema ||
            fact.OverlayContent.MediaType != ImmutableContentStoreExtensions.JsonMediaType ||
            fact.Revision < 1 || fact.FirstResponse is null || fact.OperationResult is null ||
            !NativeOwnerHistory.IsOwnRoot(owner, fact.Operation, nameof(SetCriteriaTextOverlay), (ulong)(fact.Revision - 1)))
            throw new ImmutableContentIntegrityException("The supplied overlay has another native root or revision.");
        var supplied = await content.ReadJsonAsync<CriteriaTextOverlayContent>(fact.TenantId.ToString(), fact.OverlayContent, ct)
            .ConfigureAwait(false);
        if (CriteriaTextOverlayRules.Clean(supplied) is not { } clean || clean != supplied ||
            fact.Operation.Actor.Kind != "member")
            throw new ImmutableContentIntegrityException("The supplied overlay content does not satisfy its original contract.");
        return new CriteriaTextOverlayRevision(fact.TenantId, fact.EditionId, fact.Identifier, fact.Revision,
            supplied, fact.Operation.Actor, fact.Operation.AcceptedAt);
    }
}
