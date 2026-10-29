using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Projections;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

/// <summary>Lists from the Fitz campaign directory once it has reached the source.</summary>
public sealed class ListAccessReviewCampaignsHandler(IAccessReviewCampaignDirectoryReader directory,
    IDomainEventReader events, AccessReviewReader reader, IAggregateReader aggregates,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListAccessReviewCampaigns, Page<AccessReviewCampaignSummaryView>>
{
    public async ValueTask<Result<Page<AccessReviewCampaignSummaryView>>> HandleAsync(
        IRequestContext<ListAccessReviewCampaigns> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<Page<AccessReviewCampaignSummaryView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact campaign source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<Page<AccessReviewCampaignSummaryView>>> HandleCoreAsync(
        IRequestContext<ListAccessReviewCampaigns> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.Validation, "The campaign list limit must be between 1 and 200.");
        var checkpoint = await directory.LoadCheckpointAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!await AccessReviewOutcome.IsCaughtUpAsync(events,
                AccessReviewDirectorySchema.CampaignPattern(request.TenantId), checkpoint, ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.Conflict, "The campaign directory has not reached the source.", true);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        Page<AccessReviewCampaignSummaryView> page;
        var visibleItemCounts = new Dictionary<Uuid, int>();
        try
        {
            page = await VisibleApplicationPage.ReadAsync(request.Limit ?? 50, request.Cursor,
                (limit, cursor) => directory.ListAsync(request.TenantId, limit, cursor, ct),
                async item =>
                {
                    var campaign = await aggregates.HydrateAsync(new AccessReviewCampaign(
                        request.TenantId, item.CampaignId), ct).ConfigureAwait(false);
                    if (campaign.Launched is not { } launchFact)
                        return false;
                    var launched = await reader.LaunchOfAsync(request.TenantId, launchFact, ct)
                        .ConfigureAwait(false);
                    var visibleInstances = new HashSet<Uuid>();
                    foreach (var systemInstanceId in launched.Items.Select(
                                 static reviewItem => reviewItem.SystemInstanceId).Distinct())
                    {
                        if (await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                                systemInstanceId, ct).ConfigureAwait(false))
                            visibleInstances.Add(systemInstanceId);
                    }
                    var count = launched.Items.Count(reviewItem =>
                        visibleInstances.Contains(reviewItem.SystemInstanceId));
                    visibleItemCounts[item.CampaignId] = count;
                    return count > 0;
                }, item => item.TenantId == request.TenantId).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.Validation, "The campaign cursor is invalid.");
        }
        catch (VisibleApplicationPage.ForeignDirectoryItemException)
        {
            return AccessReviewOutcome.Failure<Page<AccessReviewCampaignSummaryView>>(
                RequestErrorKind.NotFound, "The campaigns were not found.");
        }
        return Result<Page<AccessReviewCampaignSummaryView>>.Success(page with
        {
            Items = page.Items.Select(item => item with
            {
                ItemCount = visibleItemCounts[item.CampaignId],
            }).ToArray(),
        });
    }
}
