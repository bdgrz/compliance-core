using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>
///     The frozen terms of one management decision: the exact assessment it decides, the outcome and rationale, the
///     complete gap set with the plan each gap had at admission, the acknowledged gaps, the complete exact gap plan uses
///     the process must hold, and the separation-of-duties waiver proof when one was required. Every receiver copies it;
///     none re-authors it from current state. A gap plan that differs from the frozen use when it is held fences the
///     decision instead of replacing the frozen terms.
/// </summary>
public sealed record ReadinessDecisionIntent(DurableIntentHeader Header, Uuid ProgramId, Uuid AssessmentId,
    SourceFactLocator AssessmentProof, ReadinessDecisionKind DecisionKind, string Outcome, string Rationale,
    string RuleVersion, ImmutableSelectionManifest GapSelection, ImmutableSelectionManifest AcknowledgedGapIds,
    ImmutableSelectionManifest RequiredPlanUses, ImmutableContentRef? WaiverProof)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "readiness-decision-intent.v1";
}
