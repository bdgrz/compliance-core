namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record IndependenceRuleContent(int LookBackMonths,
    IReadOnlyList<IndependenceServiceRuleContent> ServiceRules, string SourceReference);
