namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>A template line and its latest published version read together at one source cut.</summary>
public sealed record MethodologyTemplatePublicationView(MethodologyTemplateLineView Line,
    MethodologyTemplatePublishedVersionView? Version);
