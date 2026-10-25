using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Reads the decision history of accepted principals from their own owner streams and content.</summary>
static class AccessClassifications
{
    /// <summary>Every decision of one hydrated principal in order.</summary>
    public static async Task<IReadOnlyList<AccessPrincipalClassificationView>> HistoryAsync(
        AccessPrincipalClassification owner, IDomainEventReader events, IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var facts = await NativeOwnerHistory.ReadAsync<AccessPrincipalClassified>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count != owner.Revision || facts.Count > 0 &&
            (owner.Latest is not { } latest || ContentDigest.OfJson(facts[^1]) != ContentDigest.OfJson(latest)))
            throw new ImmutableContentIntegrityException("The classification history differs from its hydrated owner cut.");
        var history = new List<AccessPrincipalClassificationView>(facts.Count);
        foreach (var fact in facts)
        {
            if (fact.DecisionContent.Schema != AccessReviewContent.DecisionSchema ||
                fact.DecisionContent.MediaType != ImmutableContentStoreExtensions.JsonMediaType ||
                fact.TenantId != tenantId || fact.PopulationId != owner.PopulationId ||
                !string.Equals(fact.ProviderSubjectId, owner.ProviderSubjectId, StringComparison.Ordinal) ||
                !NativeOwnerHistory.IsOwnRoot(owner, fact.Operation, nameof(ClassifyAccessPrincipal), (ulong)history.Count))
                throw new ImmutableContentIntegrityException("The classification source has another exact native scope or revision.");
            var decision = await AccessReviewContent.ReadAsync<AccessPrincipalClassificationView>(content, tenantId,
                fact.DecisionContent, ct).ConfigureAwait(false);
            if (decision.Sequence != history.Count + 1 || decision.ClassifiedBy != fact.Operation.Actor ||
                decision.ClassifiedAt != fact.Operation.AcceptedAt ||
                AccessClassificationRules.Validate(decision.Classification, decision.Rationale, decision.PersonId,
                    decision.ServiceIdentityId, decision.AccountableOwnerPersonId, decision.SharedJustification) is not null)
                throw new ImmutableContentIntegrityException("The classification body differs from its exact native decision.");
            history.Add(decision);
        }
        return history;
    }

    /// <summary>The decision history of every classifiable principal that has one, by exact provider subject id.</summary>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<AccessPrincipalClassificationView>>> ReadAsync(
        IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content, Uuid tenantId, Uuid populationId,
        IEnumerable<AccessPrincipalFact> principals, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(principals);
        var histories = new Dictionary<string, IReadOnlyList<AccessPrincipalClassificationView>>(StringComparer.Ordinal);
        foreach (var principal in principals)
        {
            if (AccessReviewVocabulary.IsAccessStructure(principal.PrincipalKind))
                continue;
            var owner = await reader.HydrateAsync(new AccessPrincipalClassification(tenantId, populationId,
                principal.ProviderSubjectId), ct).ConfigureAwait(false);
            if (owner.Revision > 0)
                histories[principal.ProviderSubjectId] = await HistoryAsync(owner, events, content, tenantId, ct)
                    .ConfigureAwait(false);
        }

        return histories;
    }
}
