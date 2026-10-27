using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     One canonical target group: every frozen row of one application in original row order, the baseline revision
///     the whole group is compared against once, and the candidate it produces. One group has one exclusive hold and
///     one preflighted native append batch.
/// </summary>
public sealed record ImportTargetGroupIntent(Uuid BatchId, Uuid ApplicationId, ulong InitialApplicationRevision,
    ImmutableSelectionManifest Rows, ImmutableContentRef CandidateContent, ulong CandidateContentRevision,
    ImmutableSelectionManifest RequiredRetirementProofs)
{
    /// <summary>Schema of the group intent content.</summary>
    public const string Schema = "import-target-group-intent.v1";
}
