using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>One signer’s exact, attributed signed-letter artifact.</summary>
public sealed record ManagementRepresentationLetterSignatureView(
    Uuid SignerMemberId,
    string SignerRole,
    Uuid SignedArtifactId,
    string SignedArtifactSha256,
    DateTimeOffset SignedAt);
