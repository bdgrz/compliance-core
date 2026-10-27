using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

static class ArtifactDisclosureSourceReader
{
    public static async ValueTask<Result<ArtifactDisclosureSourceSnapshot>> LoadAsync(
        IAggregateReader reader, Uuid tenantId, Uuid artifactId, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || artifactId == Uuid.Empty)
            return Missing();
        var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct)
            .ConfigureAwait(false);
        if (!artifact.IsCreated || artifact.Content is not { } content ||
            artifact.ContentSha256 is not { Length: 64 } sha || artifact.ContentLength is not { } length ||
            artifact.State is not { } state)
            return Missing();
        var policy = await reader.HydrateAsync(new EvidenceHandlingPolicy(tenantId, artifactId), ct)
            .ConfigureAwait(false);
        if (policy.Current is not { } current || current.TenantId != tenantId ||
            current.ArtifactId != artifactId || current.Source is not { } policySource ||
            policySource.TenantId != tenantId || policySource.SourceKind != "evidence_artifact" ||
            policySource.SourceId != artifactId ||
            !string.Equals(policySource.ContentSha256, sha, StringComparison.OrdinalIgnoreCase) ||
            current.ContentLength != length || current.SourceHandlingClass != content.HandlingClass ||
            current.SourcePosition > artifact.CommittedStreamPosition)
            return Result<ArtifactDisclosureSourceSnapshot>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The artifact has no current handling policy for its content identity."));
        return Result<ArtifactDisclosureSourceSnapshot>.Success(new(
            new ArtifactRetentionSource(tenantId, "evidence_artifact", artifactId, sha.ToLowerInvariant()),
            artifact.CommittedStreamPosition, artifact.Revision, length, state,
            current.HandlingClass, current.Revision));
    }

    public static async ValueTask<Result> CheckAsync(IAggregateReader reader,
        ArtifactDisclosureSourceSnapshot original, CancellationToken ct)
    {
        var current = await LoadAsync(reader, original.Source.TenantId, original.Source.SourceId, ct)
            .ConfigureAwait(false);
        return current.IsSuccess && current.Value == original
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evidence artifact or handling policy changed during the disclosure decision.",
                isTransient: true));
    }

    static Result<ArtifactDisclosureSourceSnapshot> Missing() =>
        Result<ArtifactDisclosureSourceSnapshot>.Failure(new RequestError(RequestErrorKind.NotFound,
            "The evidence artifact was not found in this tenant."));
}
