using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Handlers;

sealed class ReleaseEvidenceArtifactQuarantineHandler(IAggregateExecutor executor,
    IAggregateReader reader, IArtifactInspector inspector,
    EvidenceArtifactStorageReconciler reconciler, TimeProvider clock)
    : IRequestHandler<ReleaseEvidenceArtifactQuarantine>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<ReleaseEvidenceArtifactQuarantine> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Quarantine release requires a personal HTTP decision."));

        var request = context.Request;
        var actor = ActorReference.ForMember(RbacIds.Member(request.TenantId, user),
            UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        var artifact = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId,
            request.ArtifactId), ct).ConfigureAwait(false);
        if (!artifact.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The evidence artifact was not found in this tenant."));

        if (artifact.QuarantineRelease is { } prior)
        {
            if (prior.RequestId != context.RequestId || prior.Rationale != request.Rationale.Trim() ||
                prior.DecidedBy != actor)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The quarantine release decision is immutable."));
            return await ReconcileAsync(request.TenantId, request.ArtifactId, ct)
                .ConfigureAwait(false);
        }
        if (artifact.Revision != request.ExpectedRevision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evidence artifact changed before its quarantine release was decided."));
        if (artifact.State != EvidenceArtifactStates.Quarantined ||
            artifact.StateReason != EvidenceArtifactStates.Malware)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only malware-quarantined evidence can be released."));
        if (artifact.ContentSha256 is not { Length: 64 } sha || artifact.ContentLength is not { } length)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The quarantined evidence has no canonical content identity."));

        var reference = new ArtifactContentReference(request.TenantId, sha, length);
        var rescan = await inspector.InspectAsync(reference, ct).ConfigureAwait(false);
        if (rescan.State != ArtifactInspectionState.Clean)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A quarantine can be released only after a clean rescan."));
        var rescannedAt = clock.GetUtcNow();
        var current = await reader.HydrateAsync(new EvidenceArtifact(request.TenantId,
            request.ArtifactId), ct).ConfigureAwait(false);
        if (current.Revision != artifact.Revision || current.ContentSha256 != sha ||
            current.ContentLength != length || current.State != EvidenceArtifactStates.Quarantined)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evidence artifact changed during its rescan.", isTransient: true));

        var result = await executor.ExecuteAsync(new EvidenceArtifact(request.TenantId,
                request.ArtifactId), candidate => CommandFailureRequestAdapter.ToOutcome(
                candidate.ReleaseQuarantine(request.ExpectedRevision, context.RequestId, rescannedAt,
                    request.Rationale, actor, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;
        return await ReconcileAsync(request.TenantId, request.ArtifactId, ct).ConfigureAwait(false);
    }

    async ValueTask<Result> ReconcileAsync(Uuid tenantId, Uuid artifactId, CancellationToken ct)
    {
        var reconciled = await reconciler.ReconcileAsync(tenantId, artifactId, ct)
            .ConfigureAwait(false);
        return reconciled.IsSuccess ? Result.Success : Result.Failure(reconciled.Error);
    }
}
