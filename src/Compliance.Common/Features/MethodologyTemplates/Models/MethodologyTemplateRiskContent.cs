namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Complete firm-authored risk input; display summaries never substitute for scenario or effect.</summary>
public sealed record MethodologyTemplateRiskContent(string Identifier, string Title, string Scenario,
    string PotentialEffect, string? SourceNote = null);
