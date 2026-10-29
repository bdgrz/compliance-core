using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Services;

/// <summary>Requires an exact artifact read grant before issuing any content delivery location.</summary>
sealed class EvidenceArtifactDownloadAccess(IAccessGrantScopePermissionAuthorizer permissions,
    IAdvisorSharedSourceAuthorization? advisorSources = null,
    IAdvisorRecordVersionResolver? sourceVersions = null)
    : IRequestAuthorizer<IssueArtifactDownload>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IssueArtifactDownload> context,
        CancellationToken ct) => RequireAsync(context, context.Request.TenantId,
        context.Request.ArtifactId, context.Request.ProgramId,
        context.Request.AdvisorHandoffId, context.Request.SelectedSource, ct);

    public async ValueTask<Result> RequireAsync(IRequestContext context, Uuid tenantId,
        Uuid artifactId, CancellationToken ct) => await RequireAsync(context, tenantId,
        artifactId, null, null, null, ct).ConfigureAwait(false);

    public async ValueTask<Result> RequireAsync(IRequestContext context, Uuid tenantId,
        Uuid artifactId, Uuid? programId, Uuid? handoffId,
        AdvisorRecordVersion? selectedSource, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Artifact download requires a personal Bdgrz identity."));
        if (tenantId == Uuid.Empty || artifactId == Uuid.Empty)
            return Missing();
        if (programId is not null || handoffId is not null || selectedSource is not null)
        {
            if (programId is not { } scopedProgram || scopedProgram == Uuid.Empty ||
                handoffId is not { } activeHandoff || activeHandoff == Uuid.Empty ||
                selectedSource is not { IsSupported: true, SourceKind: AdvisorRecordVersion.EvidenceArtifactKind } source ||
                source.RecordId != artifactId || advisorSources is null || sourceVersions is null ||
                !await advisorSources.CanReadSelectedSourceAsync(tenantId, scopedProgram,
                    activeHandoff, source, userId, ct).ConfigureAwait(false))
                return Missing();
            var exactSource = await sourceVersions.RequireCurrentAsync(tenantId, scopedProgram,
                [source], ct).ConfigureAwait(false);
            return exactSource.IsSuccess ? Result.Success : Result.Failure(exactSource.Error);
        }
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
