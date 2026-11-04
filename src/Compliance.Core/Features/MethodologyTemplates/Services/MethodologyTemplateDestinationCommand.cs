using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>A typed destination request and its authoritative identifier-derived record identity.</summary>
public sealed record MethodologyTemplateDestinationCommand(Uuid RecordId, string RecordKind, IRequestBase Request);
