using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Pure rule replay assertions; native content publication and compact persistence are proved separately.</summary>
sealed class TypeIIPeriodRuleScenario(TypeIIPeriodRules rules)
{
    public TypeIIPeriodRules Aggregate { get; private set; } = rules;
    public IReadOnlyList<object> PendingEvents => Aggregate.PendingChanges;
    public TypeIIPeriodRuleScenario Given(IEnumerable<object> changes)
    {
        foreach (var change in changes)
            Aggregate.Apply(change);
        Aggregate.ClearPendingChanges();
        return this;
    }
}
