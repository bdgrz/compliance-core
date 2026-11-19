using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.Risks.Models;

public sealed record RiskDraftContent(string Title, string Scenario,
    string PotentialEffect, string? SourceNote)
{
    public MethodologyTemplateEntryProvenance? TemplateOrigin { get; init; }
}
