using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceRuleRatificationReadSnapshot(IndependenceRuleRatificationCatalog Owner, IndependenceRatificationContent? Content)
{
    public long Sequence => Owner.Sequence;
    public IndependenceRuleRatificationView? Active => Content?.Ratification;
    public IndependenceRuleRatificationRules Rules => IndependenceRuleRatificationRules.FromActive(Sequence, Active, Owner.Uses.HasActiveUse);
}
