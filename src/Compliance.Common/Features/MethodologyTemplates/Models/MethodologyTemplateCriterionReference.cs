using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>References licensed criteria by catalog identity without copying criteria text.</summary>
public sealed record MethodologyTemplateCriterionReference(Uuid EditionId, string Identifier);
