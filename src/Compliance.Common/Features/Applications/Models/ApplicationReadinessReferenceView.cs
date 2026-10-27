using Cntryl.Portia;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>An exact source captured by a current program readiness assessment.</summary>
public sealed record ApplicationReadinessReferenceView(Uuid ProgramId, string ProgramName,
    Uuid AssessmentId, long ReadinessRevision, string RuleVersion, DateTimeOffset AsOf,
    ReadinessSourceReference Source);
