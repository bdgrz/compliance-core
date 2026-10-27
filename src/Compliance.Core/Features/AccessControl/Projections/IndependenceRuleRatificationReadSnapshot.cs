using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

public sealed record IndependenceRuleRatificationReadSnapshot(IndependenceRuleRatificationCatalog Owner, IndependenceRatificationContent? Content)
{
    public long Sequence => Owner.Sequence;
    public IndependenceRuleRatificationView? Active => Content?.Ratification;
    public IndependenceRuleRatificationRules Rules => IndependenceRuleRatificationRules.FromActive(Sequence, Active, Owner.Uses.HasActiveUse);
}
