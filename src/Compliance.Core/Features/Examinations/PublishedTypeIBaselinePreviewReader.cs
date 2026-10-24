using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Publishes every complete canonical source body before a production baseline can freeze its references.</summary>
public sealed class PublishedTypeIBaselinePreviewReader(TypeIBaselinePreviewReader sources,
    IArtifactContentStore artifacts, IImmutableContentStore content) : ITypeIBaselinePreviewReader
{
    public ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        CancellationToken ct = default) => ReadAsync(engagement, Uuid.Empty, ct);

    public async ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        Uuid requesterUserId, CancellationToken ct = default)
    {
        if (engagement is null)
            return Result<TypeIBaselinePreview>.Failure(new RequestError(RequestErrorKind.Validation,
                "A baseline source publication requires an engagement."));
        try
        {
            using var capture = SnapshotSourceCapture.Begin(engagement.TenantId);
            var read = await sources.ReadAsync(engagement, requesterUserId, ct).ConfigureAwait(false);
            if (!read.IsSuccess)
                return read;
            var published = new List<TypeIBaselineSourceReference>(read.Value.Sources.Count);
            foreach (var source in read.Value.Sources)
            {
                if (!capture.TryGet(source.ContentSha256, out var material) || material is null)
                    return Failure("A complete canonical source body was not captured for " + source.Kind + ".", false);
                if (material.Blob.Bytes > ArtifactContentLimits.MaxContentLength)
                    return Failure("The complete source exceeds the artifact storage size limit.", false);
                await using var bytes = new FileStream(material.Path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    81920, FileOptions.Asynchronous);
                var stored = await artifacts.StoreIfAbsentAsync(engagement.TenantId, bytes, ct).ConfigureAwait(false);
                if (stored.Content.TenantId != engagement.TenantId || stored.Content.Sha256 != material.Blob.Sha256 ||
                    stored.Content.Length != material.Blob.Bytes)
                    return Failure("A published source differs from its exact captured bytes.", false);
                await using var verified = await artifacts.OpenVerifiedAsync(stored.Content, ct).ConfigureAwait(false);
                if (verified is null)
                    return Failure("A complete canonical source is unavailable in verified tenant storage.", true);
                published.Add(source with { Content = material.Blob });
            }
            var entries = published.OrderBy(source => source.Kind, StringComparer.Ordinal)
                .ThenBy(source => source.RecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(source => source.VersionId.ToString(), StringComparer.Ordinal)
                .Select(source => new SnapshotSourcePublicationEntry(source.Kind, source.RecordId, source.VersionId,
                    source.Revision, source.ContentSha256, source.Content!)).ToArray();
            var manifest = await content.WriteJsonAsync(engagement.TenantId.ToString(), Uuid.CreateVersion4(),
                SnapshotSourcePublicationManifest.Schema, new SnapshotSourcePublicationManifest(engagement.TenantId,
                    engagement.ProgramId, engagement.ExaminationEngagementId, entries), ct).ConfigureAwait(false);
            return Result<TypeIBaselinePreview>.Success(TypeIBaselinePreview.Create(read.Value.TenantId,
                read.Value.ProgramId, read.Value.ExaminationEngagementId, published, read.Value.Blockers,
                read.Value.ScopeDifferences, read.Value.SourceCoverage) with
            { SourceContentManifest = manifest });
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Failure("The canonical source publication cannot be verified.",
                exception is ImmutableContentUnavailableException);
        }
    }

    static Result<TypeIBaselinePreview> Failure(string message, bool transient) =>
        Result<TypeIBaselinePreview>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
