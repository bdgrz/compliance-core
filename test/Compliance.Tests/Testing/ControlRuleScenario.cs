using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Pure business-rule assertions; native persistence/replay is proved separately by ControlSeeds/ControlReader tests.</summary>
sealed class ControlRuleScenario
{
    readonly Uuid _tenant;
    readonly Uuid _id;
    readonly List<IControlMutationContent> _committed = [];
    IReadOnlyList<IControlMutationContent> _pending;
    public ControlRuleScenario(ControlRules source)
    {
        (_tenant, _id, Aggregate, _pending) = (source.TenantId, source.Id, source, source.PendingChanges.ToArray());
        _committed.AddRange(source.AppliedChanges.Take(source.AppliedChanges.Count - source.PendingChanges.Count));
    }
    public ControlRules Aggregate { get; private set; }
    public IReadOnlyList<IControlMutationContent> PendingEvents => _pending;
    public ulong CommittedEventCount => (ulong)_committed.Count;
    public ControlRuleScenario Given(IEnumerable<IControlMutationContent> changes)
    {
        _committed.AddRange(changes);
        _pending = [];
        Aggregate = Fold();
        return this;
    }
    public Result When(Func<ControlRules, AggregateOutcome> act)
    {
        Begin();
        var result = act(Aggregate);
        End(result.Disposition);
        return result.Result;
    }
    public Result<T> When<T>(Func<ControlRules, AggregateOutcome<T>> act)
    {
        Begin();
        var result = act(Aggregate);
        End(result.Disposition);
        return result.Result;
    }
    void Begin()
    {
        _committed.AddRange(_pending);
        _pending = [];
        Aggregate = Fold();
    }
    void End(AggregateDisposition disposition)
    {
        if (disposition == AggregateDisposition.Commit)
            _pending = Aggregate.PendingChanges.ToArray();
        else
            Aggregate = Fold();
    }
    ControlRules Fold()
    {
        var rules = new ControlRules(_tenant, _id);
        foreach (var change in _committed)
            rules.Apply(change);
        rules.ClearPendingChanges();
        return rules;
    }
}
