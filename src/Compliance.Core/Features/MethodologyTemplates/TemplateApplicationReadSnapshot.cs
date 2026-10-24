namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>An exact source-backed application read, separate from the compact mutation owner.</summary>
public sealed record TemplateApplicationReadSnapshot(TemplateApplication Owner, TemplateApplicationView? View);
