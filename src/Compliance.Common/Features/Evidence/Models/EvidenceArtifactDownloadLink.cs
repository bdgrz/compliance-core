namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>A short-lived opaque location for one authorized artifact download.</summary>
public sealed record EvidenceArtifactDownloadLink(Uri Location, DateTimeOffset ExpiresAt);
