using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Projections;

public sealed record RiskEvaluationReadSnapshot(RiskEvaluation Owner, RiskEvaluationRules Rules, IReadOnlyList<DomainEvent> Facts)
{
    public Uuid Id => Owner.Id;
    public Uuid TenantId => Owner.TenantId;
    public Uuid ProgramId => Owner.ProgramId;
    public long Revision => Owner.Revision;
    public EventStreamAddress Stream => Owner.Stream;
    public RiskTreatmentView? Treatment => Rules.Treatment;
    public RiskEvaluationView ToView() => Rules.ToView();
    public RiskAssessmentView? Latest(string phase) => Rules.Latest(phase);
    public RiskAssessmentView? FindAssessment(Uuid id) => Rules.FindAssessment(id);
    public RiskAcceptanceView? FindAcceptance(Uuid id) => Rules.FindAcceptance(id);
    public RiskAccepted? AcceptanceFact(Uuid id) => Facts.OfType<RiskAccepted>().SingleOrDefault(value => value.Operation.LogicalId == id);
}
