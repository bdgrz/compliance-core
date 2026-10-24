using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     An accepted population read back from its immutable snapshot, with its derived effective access and the
///     current classifications of its principals. The raw snapshot never changes; classifications are read from their
///     own owners.
/// </summary>
public sealed record AcceptedAccessPopulation(AccessPopulation Population,
    PopulationSnapshot Snapshot, AccessPopulationHeader Header, AccessPopulationFacts Facts,
    IReadOnlyList<EffectiveAccessView> EffectiveAccess,
    IReadOnlyDictionary<string, IReadOnlyList<AccessPrincipalClassificationView>> Classifications)
{
    public Uuid CalculationId => Population.AcceptedFact!.CalculationId;

    /// <summary>The current explicit classification decision of a principal, or null when none was recorded.</summary>
    public AccessPrincipalClassificationView? CurrentClassification(string providerSubjectId) =>
        Classifications.TryGetValue(providerSubjectId, out var history) && history.Count > 0 ? history[^1] : null;

    /// <summary>Every classification decision of a principal in order.</summary>
    public IReadOnlyList<AccessPrincipalClassificationView> History(string providerSubjectId) =>
        Classifications.TryGetValue(providerSubjectId, out var history) ? history : [];

    /// <summary>The current explicit classification, or <c>unclassified</c> when none was recorded.</summary>
    public string ClassificationOf(string providerSubjectId) =>
        CurrentClassification(providerSubjectId)?.Classification ?? AccessReviewVocabulary.Unclassified;

    public static async ValueTask<Result<AcceptedAccessPopulation>> LoadAsync(
        IAggregateReader reader, IImmutableContentStore content, Uuid tenantId, Uuid populationId,
        CancellationToken ct, bool withClassifications = true)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(content);
        var population = await reader.HydrateAsync(new AccessPopulation(tenantId, populationId), ct)
            .ConfigureAwait(false);
        if (!population.IsOpened)
            return Failure(RequestErrorKind.NotFound, "The population was not found.");
        if (population.AcceptedFact is not { } accepted)
            return Failure(RequestErrorKind.Conflict, "The population has not been accepted.");
        var stored = await PopulationSnapshotContent.ReadAsync(reader, tenantId, accepted.SnapshotId,
            AccessPopulationContent.Kind, ct).ConfigureAwait(false);
        if (!stored.IsSuccess || stored.Value.Snapshot.ContentSha256 != accepted.ContentSha256)
            return Failure(RequestErrorKind.Conflict,
                "The accepted population snapshot failed integrity verification.");
        var snapshot = stored.Value.Snapshot;
        var (header, facts) = AccessPopulationContent.Read(stored.Value.Rows);
        if (header.PopulationId != populationId)
            return Failure(RequestErrorKind.Conflict,
                "The accepted population snapshot belongs to another population.");
        var classifications = withClassifications
            ? await AccessClassifications.ReadAsync(reader, content, tenantId, populationId, facts.Principals, ct)
                .ConfigureAwait(false)
            : new Dictionary<string, IReadOnlyList<AccessPrincipalClassificationView>>();
        return Result<AcceptedAccessPopulation>.Success(new AcceptedAccessPopulation(population,
            snapshot, header, facts, AccessPopulationAnalysis.Derive(facts), classifications));
    }

    static Result<AcceptedAccessPopulation> Failure(RequestErrorKind kind, string message) =>
        Result<AcceptedAccessPopulation>.Failure(new RequestError(kind, message));
}
