using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>
///     Returns visible campaign items to access-review managers and only visible items assigned
///     to the current reviewer. Anyone without a visible item receives not found.
/// </summary>
public sealed class GetAccessReviewCampaignHandler(AccessReviewReader reader,
    IPermissionAuthorizer permissions, TimeProvider clock,
    RestrictedApplicationVisibility visibility, AccessReviewQueueEligibility eligibility)
    : IRequestHandler<GetAccessReviewCampaign, AccessReviewCampaignView>
{
    public async ValueTask<Result<AccessReviewCampaignView>> HandleAsync(
        IRequestContext<GetAccessReviewCampaign> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessReviewCampaignView>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact campaign source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<AccessReviewCampaignView>> HandleCoreAsync(
        IRequestContext<GetAccessReviewCampaign> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await reader.ReadCampaignAsync(request.TenantId, request.CampaignId, ct)
            .ConfigureAwait(false);
        if (campaign.Launched is not { } launched)
            return NotFound();
        var actor = AccessReviewActor.From(context);
        var isManager = await permissions.IsAllowedAsync(request.TenantId, actor.UserId,
            actor.MemberId, RbacPermissions.AccessReviewManage, ct).ConfigureAwait(false);
        if (!isManager && !campaign.IsReviewer(actor.MemberId))
            return NotFound();

        var permittedItems = isManager
            ? launched.Items
            : launched.Items.Where(item =>
                campaign.CurrentReviewerMemberId(item.ItemId) == actor.MemberId).ToArray();
        var visibleInstances = new HashSet<Uuid>();
        foreach (var systemInstanceId in permittedItems.Select(static item => item.SystemInstanceId)
                     .Distinct())
        {
            if (await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                    systemInstanceId, ct).ConfigureAwait(false) &&
                (isManager || launched.ProgramId is not { } programId ||
                 await eligibility.CanReviewAsync(request.TenantId, programId, actor.MemberId,
                     systemInstanceId, ct).ConfigureAwait(false)))
                visibleInstances.Add(systemInstanceId);
        }

        var visibleItemIds = permittedItems.Where(item => visibleInstances.Contains(item.SystemInstanceId))
            .Select(static item => item.ItemId).ToHashSet();
        return visibleItemIds.Count == 0
            ? NotFound()
            : Result<AccessReviewCampaignView>.Success(campaign.ToView(clock.GetUtcNow(),
                item => visibleItemIds.Contains(item.ItemId)));
    }

    static Result<AccessReviewCampaignView> NotFound() =>
        AccessReviewOutcome.Failure<AccessReviewCampaignView>(RequestErrorKind.NotFound,
            "The campaign was not found.");
}
