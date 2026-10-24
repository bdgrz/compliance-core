using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Reads the decision history of accepted principals from their own owner streams and content.</summary>
static class AccessClassifications
{
    /// <summary>Every decision of one hydrated principal in order.</summary>
    public static async Task<IReadOnlyList<AccessPrincipalClassificationView>> HistoryAsync(
        AccessPrincipalClassification owner, IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var history = new List<AccessPrincipalClassificationView>(owner.Decisions.Count);
        foreach (var decision in owner.Decisions)
            history.Add(await AccessReviewContent.ReadAsync<AccessPrincipalClassificationView>(content, tenantId,
                decision.DecisionContent, ct).ConfigureAwait(false));
        return history;
    }

    /// <summary>The decision history of every classifiable principal that has one, by exact provider subject id.</summary>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<AccessPrincipalClassificationView>>> ReadAsync(
        IAggregateReader reader, IImmutableContentStore content, Uuid tenantId, Uuid populationId,
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
                histories[principal.ProviderSubjectId] = await HistoryAsync(owner, content, tenantId, ct)
                    .ConfigureAwait(false);
        }

        return histories;
    }
}
