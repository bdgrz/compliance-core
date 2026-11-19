using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>The exact immutable assessment and current plans management considered.</summary>
public sealed record TypeIEntryAssessmentSnapshot(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    string RuleVersion, DateTimeOffset AsOf, string InputFingerprint, string Verdict,
    IReadOnlyList<ReadinessGapView> OpenGaps);
