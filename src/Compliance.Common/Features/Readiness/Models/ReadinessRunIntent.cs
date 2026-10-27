using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The frozen terms of one readiness run: the program revision it was admitted at, the adoption generation it
///     reserved, the rule version, the as-of time (frozen once when the caller omitted it) and the complete input
///     snapshot. The computation uses these and nothing else, so a replay or rerun never reads a live source.
/// </summary>
public sealed record ReadinessRunIntent(DurableIntentHeader Header, Uuid ProgramId, Uuid AssessmentId,
    ulong ExpectedProgramRevision, ulong AdoptionGeneration, string RuleVersion, DateTimeOffset AsOf,
    ImmutableContentRef Inputs, Uuid RunnerMemberId)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "readiness-run-intent.v1";
}
