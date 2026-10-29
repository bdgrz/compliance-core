using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Contracts;
using Bdgrz.Compliance.Features.Artifacts.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Readers;

/// <summary>Publishes every complete canonical source body before a production baseline can freeze its references.</summary>
public sealed class PublishedTypeIBaselinePreviewReader(TypeIBaselinePreviewReader sources,
    IArtifactContentStore artifacts, IImmutableContentStore content) : ITypeIBaselinePreviewReader
{
    public ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        CancellationToken ct = default) => ReadAsync(engagement, Uuid.Empty, ct);

    public ValueTask<Result<TypeIBaselinePreview>> ReadAsync(ExaminationEngagementView engagement,
        Uuid requesterUserId, CancellationToken ct = default) => ReadCoreAsync(engagement, requesterUserId, null, ct);

    /// <summary>Publishes effective closing sources with the separately frozen receipt cutoff.</summary>
    public ValueTask<Result<TypeIBaselinePreview>> ReadClosingAsync(ExaminationEngagementView engagement,
        Uuid requesterUserId, DateTimeOffset recordedThrough, CancellationToken ct = default) =>
        ReadCoreAsync(engagement, requesterUserId, recordedThrough, ct);

    async ValueTask<Result<TypeIBaselinePreview>> ReadCoreAsync(ExaminationEngagementView engagement,
        Uuid requesterUserId, DateTimeOffset? recordedThrough, CancellationToken ct)
    {
        if (engagement is null)
            return Result<TypeIBaselinePreview>.Failure(new RequestError(RequestErrorKind.Validation,
                "A baseline source publication requires an engagement."));
        try
        {
            using var capture = SnapshotSourceCapture.Begin(engagement.TenantId);
            var read = await sources.ReadAsync(engagement, requesterUserId, recordedThrough, ct).ConfigureAwait(false);
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
                    engagement.ProgramId, engagement.ExaminationEngagementId, entries)
                { RecordedThrough = recordedThrough, SourceCoverage = recordedThrough is null ? null : read.Value.SourceCoverage }, ct).ConfigureAwait(false);
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
