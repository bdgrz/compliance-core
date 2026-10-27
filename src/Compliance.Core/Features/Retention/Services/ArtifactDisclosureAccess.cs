using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Retention.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Retention.Services;

/// <summary>Requires both exact-artifact read access and a distinct disclosure grant.</summary>
sealed class ArtifactDisclosureAccess(IAccessGrantScopePermissionAuthorizer permissions)
    : IRequestAuthorizer<IArtifactDisclosureRequest>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IArtifactDisclosureRequest> context,
        CancellationToken ct) => RequireAsync(context, context.Request.TenantId,
        context.Request.ArtifactId, ct);

    public async ValueTask<Result> RequireAsync(IRequestContext context, Uuid tenantId,
        Uuid artifactId, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Artifact disclosure requires a personal Bdgrz identity."));
        if (tenantId == Uuid.Empty || artifactId == Uuid.Empty)
            return Missing();
        var memberId = RbacIds.Member(tenantId, userId);
        var scope = new AccessGrantScope(AccessGrantScopeKind.SharedResource, artifactId,
            EvidenceArtifactResourceTypes.Artifact);
        var canRead = await permissions.IsAllowedAtAnyScopeAsync(tenantId, userId, memberId,
            [scope], RbacPermissions.EvidenceArtifactRead, ct).ConfigureAwait(false);
        if (!canRead)
            return Missing();
        var canDisclose = await permissions.IsAllowedAtAnyScopeAsync(tenantId, userId, memberId,
            [scope], RbacPermissions.EvidenceArtifactDisclose, ct).ConfigureAwait(false);
        return canDisclose ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "The member lacks separate authorization to disclose this artifact."));
    }

    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The artifact disclosure was not found."));
}
