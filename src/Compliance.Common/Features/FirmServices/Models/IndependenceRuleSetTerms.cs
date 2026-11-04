namespace Bdgrz.Compliance.Features.FirmServices.Models;

/// <summary>Draftable classifications and the period preceding examination start.</summary>
public sealed record IndependenceRuleSetTerms(int LookBackMonths,
    IReadOnlyList<NonattestServiceClassification> Classifications)
{
    public static IndependenceRuleSetTerms Initial => new(12,
    [
        new("readiness_assessment", "conditionally_compatible"),
        new("control_design", "impairing"),
        new("control_implementation", "impairing"),
        new("control_operation", "impairing"),
        new("vciso", "impairing"),
    ]);
}
