namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceRuleCatalogReadSnapshot(IndependenceRuleCatalog Owner, IndependenceRuleVersionContent? Content)
{
    public long Sequence => Owner.Sequence;
    public IndependenceRuleVersionView? Current => Content?.Version;
    public IndependenceRuleCatalogRules Rules => IndependenceRuleCatalogRules.FromCurrent(Sequence, Current, Owner.Uses.HasActiveUse);
}
