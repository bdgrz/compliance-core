using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention.Readers;

static class EvidenceHandlingPolicySourceReader
{
    public static async ValueTask<Result<EvidenceHandlingPolicySourceSnapshot>> LoadAsync(
        IAggregateReader reader, Uuid tenantId, Uuid artifactId, CancellationToken ct)
    {
        var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct)
            .ConfigureAwait(false);
        if (!artifact.IsCreated || artifact.Content is null || artifact.ContentSha256 is null ||
            artifact.ContentLength is null || artifact.State is null)
            return Result<EvidenceHandlingPolicySourceSnapshot>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The evidence artifact was not found in this tenant."));
        var source = new ArtifactRetentionSource(tenantId, "evidence_artifact", artifactId,
            artifact.ContentSha256.ToLowerInvariant());
        return Result<EvidenceHandlingPolicySourceSnapshot>.Success(new(source,
            artifact.CommittedStreamPosition, artifact.ContentLength.Value, artifact.State,
            artifact.Content.HandlingClass));
    }

    public static async ValueTask<Result> CheckAsync(IAggregateReader reader,
        EvidenceHandlingPolicySourceSnapshot original, CancellationToken ct)
    {
        var current = await LoadAsync(reader, original.Source.TenantId,
            original.Source.SourceId, ct).ConfigureAwait(false);
        return current.IsSuccess && current.Value == original
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evidence artifact changed during the handling-policy decision.", isTransient: true));
    }
}
