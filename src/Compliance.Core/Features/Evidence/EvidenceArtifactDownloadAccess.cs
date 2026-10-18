using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Requires an exact artifact read grant before issuing any content delivery location.</summary>
sealed class EvidenceArtifactDownloadAccess(IAccessGrantScopePermissionAuthorizer permissions)
    : IRequestAuthorizer<IssueArtifactDownload>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IssueArtifactDownload> context,
        CancellationToken ct) => RequireAsync(context, context.Request.TenantId,
        context.Request.ArtifactId, ct);

    public async ValueTask<Result> RequireAsync(IRequestContext context, Uuid tenantId,
        Uuid artifactId, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Artifact download requires a personal Bdgrz identity."));
        if (tenantId == Uuid.Empty || artifactId == Uuid.Empty)
            return Missing();
        var scope = new AccessGrantScope(AccessGrantScopeKind.SharedResource, artifactId,
            EvidenceArtifactResourceTypes.Artifact);
        return await permissions.IsAllowedAtAnyScopeAsync(tenantId, userId,
                RbacIds.Member(tenantId, userId), [scope], RbacPermissions.EvidenceArtifactRead, ct)
            .ConfigureAwait(false)
            ? Result.Success
            : Missing();
    }

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The artifact was not found or the actor lacks its exact read grant."));
}
