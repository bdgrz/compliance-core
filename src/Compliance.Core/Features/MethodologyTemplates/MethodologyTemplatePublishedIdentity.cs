using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Immutable lookup of an exact published version identity, never a current-state authorization source.</summary>
public sealed record MethodologyTemplatePublishedIdentity(Uuid VersionId, Uuid TemplateId, long VersionNumber,
    DateTimeOffset PublishedAt);
