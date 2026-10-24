using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Combines feature-owned source reads and reports every uncovered family as a blocker.</summary>
public sealed class TypeIBaselinePreviewReader : ITypeIBaselinePreviewReader
{
    const int MaximumSources = 256;
    readonly Dictionary<string, ITypeIBaselineSourceContributor> _contributors;

    public TypeIBaselinePreviewReader(IEnumerable<ITypeIBaselineSourceContributor> contributors)
    {
        var required = TypeIBaselineSourceFamilies.Required.ToHashSet(StringComparer.Ordinal);
        var registered = new Dictionary<string, ITypeIBaselineSourceContributor>(
            StringComparer.Ordinal);
        foreach (var contributor in contributors)
        {
            if (contributor is null || string.IsNullOrWhiteSpace(contributor.Family) ||
                !required.Contains(contributor.Family) ||
                !registered.TryAdd(contributor.Family, contributor))
                throw new InvalidOperationException(
                    "Type I baseline source contributors must uniquely cover a required family.");
        }
        _contributors = registered;
    }

    public async ValueTask<Result<TypeIBaselinePreview>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct = default) =>
        await ReadAsync(engagement, Uuid.Empty, ct).ConfigureAwait(false);

    public ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        Uuid requesterUserId, CancellationToken ct = default) => ReadAsync(engagement, requesterUserId, null, ct);

    /// <summary>Separates the selected effective date from an explicit server-frozen close receipt cutoff.</summary>
    public async ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        Uuid requesterUserId, DateTimeOffset? recordedThrough, CancellationToken ct = default)
    {
        if (engagement is null || engagement.TenantId == Uuid.Empty ||
            engagement.ProgramId == Uuid.Empty || engagement.ExaminationEngagementId == Uuid.Empty ||
            engagement.BoundaryId == Uuid.Empty)
            return Result<TypeIBaselinePreview>.Failure(new RequestError(RequestErrorKind.Validation,
                "A Type I baseline preview requires a complete engagement identity."));

        var sources = new List<TypeIBaselineSourceReference>();
        var sourceCoverage = new List<TypeIBaselineSourceCoverage>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var family in TypeIBaselineSourceFamilies.Required)
        {
            if (!_contributors.TryGetValue(family, out var contributor))
            {
                blockers.Add($"The {family} source family is not captured by the Type I baseline preview.");
                continue;
            }

            var context = new TypeIBaselineSourceReadContext(requesterUserId,
                Array.AsReadOnly(sources.ToArray()))
            { RecordedThrough = recordedThrough };
            var result = await contributor.ReadAsync(engagement, context, ct)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return Result<TypeIBaselinePreview>.Failure(result.Error);
            var contribution = result.Value;
            if (contribution is null || contribution.Family != family ||
                contribution.Sources is null || contribution.Blockers is null ||
                contribution.ScopeDifferences is null)
                return Result<TypeIBaselinePreview>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    $"The {family} source family returned an invalid baseline contribution."));

            sources.AddRange(contribution.Sources);
            sourceCoverage.Add(TypeIBaselineContentIdentity.Coverage(family,
                contribution.Sources));
            blockers.AddRange(contribution.Blockers);
            scopeDifferences.AddRange(contribution.ScopeDifferences);
        }

        var approvedBoundaries = sources.Where(static source =>
            source.Kind == "approved_boundary").ToArray();
        if (approvedBoundaries.Length != 1 ||
            approvedBoundaries[0].RecordId != engagement.BoundaryId)
            blockers.Add("The preview does not contain the engagement's exact approved boundary.");
        if (sources.Count > MaximumSources)
            blockers.Add("The Type I baseline exceeds its supported source-reference limit.");

        return Result<TypeIBaselinePreview>.Success(TypeIBaselinePreview.Create(
            engagement.TenantId, engagement.ProgramId, engagement.ExaminationEngagementId,
            sources, blockers, scopeDifferences, sourceCoverage));
    }
}
