using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>References licensed criteria by catalog identity without copying criteria text.</summary>
public sealed record MethodologyTemplateCriterionReference(Uuid EditionId, string Identifier);
