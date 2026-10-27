using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
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
