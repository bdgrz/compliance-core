using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

public sealed class PreviewBulkAccessDecisionHandler(AccessReviewReader reader,
    RestrictedApplicationVisibility visibility, AccessReviewQueueEligibility eligibility)
    : IRequestHandler<PreviewBulkAccessDecision, BulkAccessDecisionPreview>
{
    public async ValueTask<Result<BulkAccessDecisionPreview>> HandleAsync(
        IRequestContext<PreviewBulkAccessDecision> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<BulkAccessDecisionPreview>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact campaign source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<BulkAccessDecisionPreview>> HandleCoreAsync(
        IRequestContext<PreviewBulkAccessDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        var campaign = await reader.ReadCampaignAsync(request.TenantId, request.CampaignId, ct)
            .ConfigureAwait(false);
        var actor = AccessReviewActor.From(context);
        if (campaign.Launched is not { } launched)
            return AccessReviewOutcome.Failure<BulkAccessDecisionPreview>(RequestErrorKind.NotFound,
                "The campaign was not found.");
        foreach (var itemId in request.ItemIds.Distinct())
        {
            var item = launched.Items.FirstOrDefault(candidate => candidate.ItemId == itemId);
            if (item is null || !await visibility.CanReadSystemInstanceAsync(request.TenantId,
                    actor.UserId, item.SystemInstanceId, ct).ConfigureAwait(false) ||
                campaign.CurrentReviewerMemberId(itemId) != actor.MemberId ||
                launched.ProgramId is { } programId &&
                !await eligibility.CanReviewAsync(request.TenantId, programId, actor.MemberId,
                    item.SystemInstanceId, ct).ConfigureAwait(false))
                return AccessReviewOutcome.Failure<BulkAccessDecisionPreview>(
                    RequestErrorKind.NotFound, "The campaign was not found.");
        }
        return BulkAccessDecisions.Preview(campaign, request.ItemIds, request.Decision,
            actor.MemberId, actor.UserId);
    }
}
