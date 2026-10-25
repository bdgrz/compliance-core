namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskDraftReadSnapshot(RiskDraft Owner, RiskDraftRules Rules, RiskDraftRevisionView? Current)
{
    public bool IsCreated => Current is not null;
    public RiskDraftView? View => Current is null ? null : RiskDraftFactContent.View(Current);
}
