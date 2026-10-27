using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Kernel;

namespace Bdgrz.Compliance.Features.Commitments;

/// <summary>Creation compares the normalized identity and authored terms that the existing commitment business contract preserves.</summary>
static class CommitmentCreateIntent
{
    public static string Digest(CreateCommitmentDraft request) => AuthoredIntentDigest.Compute(request with
    {
        Kind = CommitmentDraft.NormalizeKind(request.Kind),
        Identifier = CommitmentDraft.NormalizeIdentifier(request.Identifier),
        Statement = request.Statement?.Trim()!,
        Context = request.Context?.Trim()!,
        SourceReference = request.SourceReference?.Trim()!,
    });
}
