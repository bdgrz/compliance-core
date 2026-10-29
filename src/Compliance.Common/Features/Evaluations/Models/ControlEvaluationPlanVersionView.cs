using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Evaluations.Models;

public sealed record ControlEvaluationPlanVersionView(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid ControlVersionId, Uuid PlanVersionId, long Version, string Objective,
    IReadOnlyList<EvaluationProcedureStep> Steps, bool TesterIndependenceRequired,
    Uuid AuthoredByMemberId, ActorReference Author, DateTimeOffset DefinedAt);
