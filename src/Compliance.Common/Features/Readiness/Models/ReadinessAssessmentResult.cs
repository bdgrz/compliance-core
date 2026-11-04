using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>The exact result and frozen source inputs of one product-rule evaluation.</summary>
public sealed record ReadinessAssessmentResult(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    string RuleVersion, DateTimeOffset AsOf, string InputFingerprint, ImmutableContentRef InputSnapshot,
    IReadOnlyList<ReadinessSourceReference> SourceRecordVersions, ReadinessCompleteness Completeness,
    IReadOnlyList<ReadinessInputView> Inputs, IReadOnlyList<ReadinessFindingView> Findings,
    IReadOnlyList<ReadinessGapView> Gaps, ActorReference RequestedBy, DateTimeOffset RecordedAt,
    DateOnly? ScheduledDay)
{
    public IReadOnlyList<ReadinessComputedGapSet> ComputedInputs { get; init; } = [];

    public const string Schema = "readiness-assessment-result.v2";
}
