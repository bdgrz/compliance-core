using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Risks.Models;

/// <summary>One attributable, immutable change to a risk's evaluation.</summary>
public sealed record RiskEvaluationHistoryEntryView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, string Kind, RiskAssessmentView? Assessment, RiskTreatmentView? Treatment,
    RiskAcceptanceView? Acceptance, ActorReference Actor, DateTimeOffset OccurredAt);
