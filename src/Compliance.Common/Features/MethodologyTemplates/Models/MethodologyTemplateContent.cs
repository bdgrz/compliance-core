namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>One authored template version; criteria are references, never licensed source text.</summary>
public sealed record MethodologyTemplateContent(
    string Name,
    string Description,
    string TemplateKind,
    IReadOnlyList<MethodologyTemplateRecordContent> Records);
