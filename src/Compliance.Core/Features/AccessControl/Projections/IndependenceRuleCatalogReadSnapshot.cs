using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

public sealed record IndependenceRuleCatalogReadSnapshot(IndependenceRuleCatalog Owner, IndependenceRuleVersionContent? Content)
{
    public long Sequence => Owner.Sequence;
    public IndependenceRuleVersionView? Current => Content?.Version;
    public IndependenceRuleCatalogRules Rules => IndependenceRuleCatalogRules.FromCurrent(Sequence, Current, Owner.Uses.HasActiveUse);
}
