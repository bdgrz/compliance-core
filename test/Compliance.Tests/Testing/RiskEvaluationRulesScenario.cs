using Bdgrz.Compliance.Features.Risks;
namespace Bdgrz.Compliance.Tests.Testing;
/// <summary>Exercises the transient business rules over actual structured mutation bodies.</summary>
sealed class RiskEvaluationRulesScenario(RiskEvaluationRules rules)
{
    public RiskEvaluationRules Aggregate => rules;
    public IReadOnlyList<object> PendingEvents => rules.PendingChanges;
    public RiskEvaluationRulesScenario Given(params object[] facts)
    {
        foreach (var fact in facts)
            rules.Apply(fact);
        rules.ClearPendingChanges();
        return this;
    }
}
