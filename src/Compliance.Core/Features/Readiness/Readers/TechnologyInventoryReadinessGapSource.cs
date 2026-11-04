using System.Globalization;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Readers;

/// <summary>Concrete adapter for the exact technology revisions already resolved from program source history.</summary>
public sealed class TechnologyInventoryReadinessGapSource(IReadinessSourceReader sources) : IReadinessComputedGapSource
{
    public string Family => "technology_inventory";

    public async ValueTask<Result<ReadinessComputedGapSet>> ReadAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        var source = await sources.ReadAsync(tenantId, programId, asOf, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<ReadinessComputedGapSet>.Failure(source.Error);
        if (source.Value.TruncatedFamilies.Contains("technology_inventory", StringComparer.Ordinal))
            return Result<ReadinessComputedGapSet>.Success(new ReadinessComputedGapSet(Family,
                [new(Family, "truncated", source.Value.TechnologyInventory.Count, "The complete technology source population is unavailable.")], [], []));
        return Result<ReadinessComputedGapSet>.Success(Evaluate(programId, source.Value.TechnologyInventory));
    }

    public static ReadinessComputedGapSet Evaluate(Uuid programId, IReadOnlyList<ReadinessTechnologyInventoryInput> records)
    {
        const string family = "technology_inventory";
        var inputs = new List<ReadinessInputView>();
        var gaps = new List<ReadinessGapView>();
        var versions = new List<ReadinessSourceReference>();
        foreach (var record in records.OrderBy(static record => record.RecordId.ToString(), StringComparer.Ordinal))
        {
            var status = record.Revision is null || record.Lifecycle is null ? "unknown" :
                record.Lifecycle == "draft" ? "draft" : "assessed";
            inputs.Add(new ReadinessInputView(family, status, 1,
                $"The {record.SubjectType} source {record.RecordId} is {record.Lifecycle ?? "unknown"} at the assessment instant."));
            if (record.Revision is { } revision)
                versions.Add(new ReadinessSourceReference(record.SubjectType, record.RecordId, revision.ToString(CultureInfo.InvariantCulture)));
            if (status == "assessed" && record.Lifecycle != "active")
                gaps.Add(new ReadinessGapView(ReadinessRules.GapIdFor(programId, ReadinessRules.TechnologyInventoryActive,
                    $"{record.SubjectType}|{record.RecordId}"), "technology_inventory_record_inactive_at_as_of",
                    record.RecordId.ToString(), ReadinessRules.TechnologyInventoryActive,
                    "The technology source referenced by the program is not active at the assessment instant.",
                    versions.Where(version => version.Id == record.RecordId).ToArray()));
        }
        if (inputs.Count == 0)
            inputs.Add(new ReadinessInputView(family, "assessed", 0, "The resolved program boundary references no technology records."));
        return new ReadinessComputedGapSet(family, inputs, gaps, versions);
    }
}
