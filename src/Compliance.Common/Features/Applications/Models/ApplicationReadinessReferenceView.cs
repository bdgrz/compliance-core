using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>An exact source captured by a current program readiness assessment.</summary>
public sealed record ApplicationReadinessReferenceView(Uuid ProgramId, string ProgramName,
    Uuid AssessmentId, long ReadinessRevision, string RuleVersion, DateTimeOffset AsOf,
    ReadinessSourceReference Source);
