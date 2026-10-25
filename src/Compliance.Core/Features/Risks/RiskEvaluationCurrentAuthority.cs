using Bdgrz.Compliance.Features.AccessControl;
namespace Bdgrz.Compliance.Features.Risks;
/// <summary>Three current assessment slots and the current decisions, stored as immutable content.</summary>
public sealed record RiskEvaluationCurrentAuthority(RiskAssessmentView? Inherent, RiskAssessmentView? Target,
    RiskAssessmentView? Residual, RiskTreatmentView? Treatment, RiskAcceptanceView? LatestAcceptance,
    ActorReference? LastChangedBy, DateTimeOffset? LastChangedAt);
