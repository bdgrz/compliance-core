using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Commands;
namespace Bdgrz.Compliance.Features.Commitments.Services;

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
