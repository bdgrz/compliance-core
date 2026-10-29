using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Queries;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

public sealed class ListInformationAssetBoundaryReferencesHandler(
    IAggregateReader aggregates, IApplicationBoundaryReferenceDirectory directory,
    ApplicationBoundaryReferenceReadConsistency consistency,
    TechnologyInventoryRestrictedVisibility visibility)
    : IRequestHandler<ListInformationAssetBoundaryReferences,
        Page<ApplicationBoundaryReferenceView>>
{
    public async ValueTask<Result<Page<ApplicationBoundaryReferenceView>>> HandleAsync(
        IRequestContext<ListInformationAssetBoundaryReferences> context, CancellationToken ct)
    {
        var request = context.Request;
        if (InventoryBoundaryReferenceReader.RejectLimit(request.Limit, "information asset")
            is { } rejected)
            return rejected;
        var asset = await aggregates.HydrateAsync(new InformationAsset(
            request.TenantId, request.InformationAssetId), ct).ConfigureAwait(false);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        var canRead = asset.IsCreated && await visibility.CanReadAssetAsync(request.TenantId,
            userId, asset.Id, asset.Content?.Classification, ct).ConfigureAwait(false);
        if (!canRead)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The information asset was not found."));
        var revision = asset.Revision;
        var page = await InventoryBoundaryReferenceReader.ListAsync(true,
            "information asset", "information", request.TenantId, request.InformationAssetId,
            request.Limit, request.Cursor, directory, consistency, ct).ConfigureAwait(false);
        var current = await aggregates.HydrateAsync(new InformationAsset(
            request.TenantId, request.InformationAssetId), ct).ConfigureAwait(false);
        if (!current.IsCreated || !await visibility.CanReadAssetAsync(request.TenantId, userId,
                current.Id, current.Content?.Classification, ct).ConfigureAwait(false))
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The information asset was not found."));
        if (page.IsSuccess && current.Revision != revision)
            return Result<Page<ApplicationBoundaryReferenceView>>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The information asset changed while its references were being read. Retry the query.",
                isTransient: true));
        return page;
    }
}
