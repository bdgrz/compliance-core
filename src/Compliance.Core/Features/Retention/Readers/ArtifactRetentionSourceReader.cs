using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Retention.Projections;
using Bdgrz.Compliance.Features.Retention.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Retention.Readers;

static class ArtifactRetentionSourceReader
{
    public static async ValueTask<Result<ArtifactRetentionSourceSnapshot>> LoadAsync(IAggregateReader reader,
        Uuid tenantId, string kind, Uuid id, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || id == Uuid.Empty || !ArtifactRetentionRules.IsSupportedSource(kind))
            return Result<ArtifactRetentionSourceSnapshot>.Failure(new RequestError(RequestErrorKind.Validation,
                "Retention requires a supported tenant source."));
        ArtifactRetentionSourceSnapshot? snapshot = null;
        if (kind == "evidence_artifact")
        {
            var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, id), ct).ConfigureAwait(false);
            if (artifact.IsCreated && artifact.Registration is { } registration && registration.TenantId == tenantId &&
                registration.ArtifactId == id && artifact.Content is { } content && artifact.ContentSha256 is { } sha)
                snapshot = new(new(tenantId, kind, id, sha.ToLowerInvariant()), artifact.CommittedStreamPosition,
                    content.PeriodStart, content.PeriodEnd);
        }
        else
        {
            var batch = await reader.HydrateAsync(new ApplicationImportBatch(tenantId, id), ct).ConfigureAwait(false);
            // Retention binds the immutable staged observation, so its position is that of the staging fact: later
            // lifecycle facts of the batch never change what is retained.
            if (batch.IsCreated && batch.Staged is { Metadata: { } stagedMetadata } staged && staged.TenantId == tenantId &&
                staged.BatchId == id && batch.ContentDigest is { } sha)
                snapshot = new(new(tenantId, kind, id, sha.ToLowerInvariant()), stagedMetadata.AggregateVersion);
        }
        return snapshot is not null
            ? Result<ArtifactRetentionSourceSnapshot>.Success(snapshot)
            : Result<ArtifactRetentionSourceSnapshot>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The retained source was not found."));
    }

    public static async ValueTask<Result> CheckAsync(IAggregateReader reader,
        ArtifactRetentionSourceSnapshot original, CancellationToken ct)
    {
        var current = await LoadAsync(reader, original.Source.TenantId, original.Source.SourceKind,
            original.Source.SourceId, ct).ConfigureAwait(false);
        return current.IsSuccess && current.Value == original ? Result.Success : Result.Failure(
            new RequestError(RequestErrorKind.Conflict, "The retained source changed during this request.", isTransient: true));
    }
}
