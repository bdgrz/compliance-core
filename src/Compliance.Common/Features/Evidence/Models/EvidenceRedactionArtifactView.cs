using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Evidence.Models;

/// <summary>Permitted identity summary without original registration or event-envelope proof.</summary>
public sealed record EvidenceRedactionArtifactView(Uuid ArtifactId, string ContentSha256,
    long ContentLength, DateTimeOffset RegisteredAt);
