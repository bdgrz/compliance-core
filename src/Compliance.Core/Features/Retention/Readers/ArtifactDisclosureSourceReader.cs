using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
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
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Projections;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Retention.Readers;

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
