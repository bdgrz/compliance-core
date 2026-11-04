using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Frozen original platform authoring intent for one template creation operation.</summary>
public sealed record MethodologyTemplateCreationIntent(Uuid CommandId, Uuid TemplateId, string Name,
    string Purpose, Uuid CreatedBy);
