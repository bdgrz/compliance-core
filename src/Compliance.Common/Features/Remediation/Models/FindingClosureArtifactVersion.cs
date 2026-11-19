using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>One exact immutable artifact content version offered as finding-resolution evidence.</summary>
public sealed record FindingClosureArtifactVersion(Uuid ArtifactId, string ContentSha256);
