using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

sealed class CurrentRatifiedIndependenceRulesReader(IndependenceRuleCatalogReader rules,
    IndependenceRuleRatificationReader ratifications)
{
    public async ValueTask<IndependenceRuleVersionView?> ReadActiveAsync(CancellationToken ct)
    {
        var current = await ratifications.ReadAsync(ct).ConfigureAwait(false);
        if (current.Active is not { } active)
            return null;
        // Ratification was an authorized historical decision. Later staff or duty changes
        // affect new decisions, while the immutable ratified version remains in force.
        var draft = await rules.FindVersionAsync(active.RuleVersion, ct).ConfigureAwait(false);
        if (draft is null)
            return null;
        var projected = IndependenceRuleVersionProjection.ApplyRatification(draft, active);
        return projected.IsRatified ? projected : null;
    }
}
