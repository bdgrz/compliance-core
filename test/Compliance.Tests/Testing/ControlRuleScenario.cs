using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
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
