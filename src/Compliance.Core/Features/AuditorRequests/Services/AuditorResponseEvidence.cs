using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Projections;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AuditorRequests.Services;

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
