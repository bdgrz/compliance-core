using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

sealed class AuditorResponseEvidence(EvidenceArtifactDownloadAccess access,
    EvidenceArtifactMetadataRead metadata, TimeProvider clock)
{
    public async ValueTask<Result> RequireAvailableAsync(IRequestContext context, Uuid tenantId,
        IReadOnlyList<Uuid>? artifactIds, CancellationToken ct)
    {
        if (artifactIds is null || artifactIds.Count > 64 ||
            artifactIds.Any(static id => id == Uuid.Empty) ||
            artifactIds.Distinct().Count() != artifactIds.Count)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Response evidence must use unique, nonempty artifact identities."));

        var captured = new List<EvidenceArtifactMetadataSnapshot>(artifactIds.Count);
        foreach (var artifactId in artifactIds)
        {
            var allowed = await access.RequireAsync(context, tenantId, artifactId, ct)
                .ConfigureAwait(false);
            if (!allowed.IsSuccess)
                return allowed;
            var result = await metadata.GetCapturedAsync(tenantId, artifactId, ct)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return Result.Failure(result.Error);
            if (result.Value.Metadata.State != EvidenceArtifactStates.Available ||
                result.Value.AvailabilityRecordedAt is not { } availableAt ||
                availableAt > clock.GetUtcNow())
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Response evidence must be clean and available before it can be included."));
            captured.Add(result.Value);
        }

        foreach (var item in captured)
        {
            var allowed = await access.RequireAsync(context, tenantId,
                item.Metadata.ArtifactId, ct).ConfigureAwait(false);
            if (!allowed.IsSuccess)
                return allowed;
            var unchanged = await metadata.CheckAsync(item.Metadata, ct).ConfigureAwait(false);
            if (!unchanged.IsSuccess)
                return unchanged;
        }
        return Result.Success;
    }
}
