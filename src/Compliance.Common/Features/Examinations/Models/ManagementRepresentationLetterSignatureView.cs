using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>One signer’s exact, attributed signed-letter artifact.</summary>
public sealed record ManagementRepresentationLetterSignatureView(
    Uuid SignerMemberId,
    string SignerRole,
    Uuid SignedArtifactId,
    string SignedArtifactSha256,
    DateTimeOffset SignedAt);
