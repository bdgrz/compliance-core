namespace Bdgrz.Compliance.Features.Risks.Models;

public sealed record RiskDraftContent(string Title, string Scenario,
    string PotentialEffect, string? SourceNote);
