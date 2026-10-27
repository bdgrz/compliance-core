using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks.Models;
/// <summary>Bounded current identity and hashes of source-composed history; no authored bodies or history are owner state.</summary>
public sealed record RiskEvaluationAuthorityHeader(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision,
    Uuid? InherentAssessmentId, Uuid? InherentMethodVersionId, Uuid? ResidualAssessmentId, string? TreatmentKind,
    Uuid? LatestAcceptanceId, long AssessmentCount, long AcceptanceCount, string ViewSha256);
