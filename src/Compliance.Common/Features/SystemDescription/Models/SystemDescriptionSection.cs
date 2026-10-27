namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>One required narrative section and its explicit completeness decision.</summary>
public sealed record SystemDescriptionSection(string SectionId, string Title, string Content,
    string Status, string? NotApplicableRationale);
