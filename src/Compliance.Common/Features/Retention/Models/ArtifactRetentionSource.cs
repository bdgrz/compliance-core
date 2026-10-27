using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Models;

/// <summary>A governance reference to existing content, never a second copy of it.</summary>
public sealed record ArtifactRetentionSource(Uuid TenantId, string SourceKind, Uuid SourceId, string ContentSha256);
