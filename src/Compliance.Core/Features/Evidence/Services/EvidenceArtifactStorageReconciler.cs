using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Contracts;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Services;

/// <summary>
///     Finishes a durable inspection verdict using the current canonical registration. Source,
///     content and checkpoints are separate resources; a later source transition can race an effect.
/// </summary>
public sealed class EvidenceArtifactStorageReconciler(IArtifactContentStore store, IAggregateReader reader)
{
    public async ValueTask<Result<EvidenceArtifact>> ReconcileAsync(Uuid tenantId, Uuid artifactId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct)
            .ConfigureAwait(false);
        var registration = artifact.Registration;
        if (tenantId == Uuid.Empty || artifactId == Uuid.Empty || registration is null ||
            registration.TenantId != tenantId || registration.ArtifactId != artifactId ||
            registration.Content is null ||
            EvidenceRules.Validate(registration.Content, registration.ContentSha256, registration.ContentLength) is not null)
            return Failure("The evidence artifact has no valid canonical content registration.");
        var reference = new ArtifactContentReference(registration.TenantId,
            registration.ContentSha256, registration.ContentLength);
        ct.ThrowIfCancellationRequested();
        switch (artifact.State)
        {
            case EvidenceArtifactStates.Quarantined when artifact.StateReason == EvidenceArtifactStates.Malware:
                if (!await store.QuarantineAsync(reference, ct).ConfigureAwait(false))
                {
                    await using var verified = await store.OpenQuarantinedAsync(reference, ct).ConfigureAwait(false);
                    if (verified is null)
                        return Failure("The evidence content could not be quarantined.");
                }
                break;
            case EvidenceArtifactStates.Rejected when artifact.StateReason is
                EvidenceArtifactStates.SecretDetected or EvidenceArtifactStates.Invalid:
                if (await store.DeleteAsync(reference, ct).ConfigureAwait(false) is not
                    (ArtifactDeletionResult.Deleted or ArtifactDeletionResult.NotFound))
                    return Failure("The rejected evidence content could not be purged.");
                break;
            case EvidenceArtifactStates.PendingInspection:
            case EvidenceArtifactStates.Disposed:
                break;
            case EvidenceArtifactStates.Available when artifact.QuarantineReleaseRequestId is not null:
                if (!await store.ReleaseQuarantineAsync(reference, ct).ConfigureAwait(false))
                {
                    await using var verified = await store.OpenVerifiedAsync(reference, ct)
                        .ConfigureAwait(false);
                    if (verified is null)
                        return Failure("The cleanly rescanned evidence content could not be released from quarantine.");
                }
                break;
            case EvidenceArtifactStates.Available:
                break;
            default:
                return Failure("The evidence artifact has an unrecognized inspection state or reason.");
        }
        ct.ThrowIfCancellationRequested();
        return Result<EvidenceArtifact>.Success(artifact);
    }

    static Result<EvidenceArtifact> Failure(string message) =>
        Result<EvidenceArtifact>.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
