using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Projections;

/// <summary>Builds and orders population rows for the access-review snapshot kinds.</summary>
static class SnapshotRows
{
    public static PopulationRow Row<T>(string key, T value, JsonTypeInfo<T> type) =>
        new(key, JsonSerializer.SerializeToElement(value, type));

    public static void Sort(List<PopulationRow> rows) =>
        rows.Sort(static (left, right) => SnapshotContentIdentity.CompareCodePoints(
            SnapshotContentIdentity.NormalizeText(left.Key),
            SnapshotContentIdentity.NormalizeText(right.Key)));
}
