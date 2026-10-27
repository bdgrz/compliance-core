using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>Identity and exact derived-body digest only; historical narrative stays in its native source content.</summary>
public sealed record SystemDescriptionVersionToken(Uuid VersionId, long Version, string Status, string Sha256);
