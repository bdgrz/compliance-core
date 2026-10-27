using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

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

    public static ValueTask<Result<AcceptedAccessPopulation>> LoadAsync(
        IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content, Uuid tenantId,
        Uuid populationId, CancellationToken ct) => LoadCoreAsync(reader, events, content, tenantId, populationId, true, ct);

    public static ValueTask<Result<AcceptedAccessPopulation>> LoadAsync(
        IAggregateReader reader, IImmutableContentStore content, Uuid tenantId, Uuid populationId,
        CancellationToken ct) => LoadCoreAsync(reader, null, content, tenantId, populationId, false, ct);

    static async ValueTask<Result<AcceptedAccessPopulation>> LoadCoreAsync(
        IAggregateReader reader, IDomainEventReader? events, IImmutableContentStore content, Uuid tenantId,
        Uuid populationId, bool withClassifications, CancellationToken ct)
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
        IReadOnlyDictionary<string, IReadOnlyList<AccessPrincipalClassificationView>> classifications;
        try
        {
            classifications = withClassifications
                ? await AccessClassifications.ReadAsync(reader, events!, content, tenantId, populationId, facts.Principals, ct)
                    .ConfigureAwait(false)
                : new Dictionary<string, IReadOnlyList<AccessPrincipalClassificationView>>();
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AcceptedAccessPopulation>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact classification source cannot be verified.", error is ImmutableContentUnavailableException));
        }
        return Result<AcceptedAccessPopulation>.Success(new AcceptedAccessPopulation(population,
            snapshot, header, facts, AccessPopulationAnalysis.Derive(facts), classifications));
    }

    static Result<AcceptedAccessPopulation> Failure(RequestErrorKind kind, string message) =>
        Result<AcceptedAccessPopulation>.Failure(new RequestError(kind, message));
}
