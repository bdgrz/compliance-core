using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Stable discovery identity only; current/publication content is resolved from its native owner.</summary>
public sealed record MethodologyTemplateCatalogIdentity(Uuid TemplateId, DateTimeOffset FirstPublishedAt);
