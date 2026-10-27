using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>
///     One source claim: the last published observation of one source record. It is distinct from the governed
///     application fields. A frozen plan carries every claim it was frozen against.
/// </summary>
public sealed record ApplicationImportSourceClaimRecord(Uuid LastObservedBatchId, ApplicationImportPlannedRow Observation,
    DateTimeOffset CommittedAt)
{
    /// <summary>Schema of one item of the source-claims selection.</summary>
    public const string ItemSchema = "import-source-claim.v1";
}
