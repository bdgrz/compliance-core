using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Risks.Models;
/// <summary>Three current assessment slots and the current decisions, stored as immutable content.</summary>
public sealed record RiskEvaluationCurrentAuthority(RiskAssessmentView? Inherent, RiskAssessmentView? Target,
    RiskAssessmentView? Residual, RiskTreatmentView? Treatment, RiskAcceptanceView? LatestAcceptance,
    ActorReference? LastChangedBy, DateTimeOffset? LastChangedAt);
