namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Compact publication authority and the exact selected immutable published version.</summary>
public sealed record MethodologyTemplatePublicationSnapshot(MethodologyTemplate Owner,
    MethodologyTemplateVersionView? Version);
