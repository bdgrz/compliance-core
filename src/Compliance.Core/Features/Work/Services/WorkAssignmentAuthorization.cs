using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.Work.Services;

/// <summary>Reauthorizes an original receipt through current identities and source restrictions, never cached decisions.</summary>
public sealed class WorkAssignmentAuthorization(WorkQueueReader queue, AccessReviewReader reviews,
    PolicyAcknowledgementRecorderGuard acknowledgements)
{
    internal async ValueTask<Result> AuthorizeAsync(Uuid tenantId, Uuid programId, OperationsActor actor,
        WorkAssignmentSourceIdentity source, string commandKind, CancellationToken ct)
    {
        if (source.TenantId != tenantId || source.ProgramId != programId || source.WorkItemId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The captured work source differs from this exact request scope."));
        var candidate = source.Describe();
        if (AccessReviewCampaignWork.IsReview(source.Kind) || AccessReviewCampaignWork.IsRemediation(source.Kind))
        {
            if (source.AccessReviewCampaignId is not { } campaignId)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The captured access-review work has no authoritative campaign identity."));
            var campaign = await reviews.ReadCampaignIdentityAsync(tenantId, campaignId, Uuid.Empty, ct).ConfigureAwait(false);
            var item = campaign.Launched?.Items.SingleOrDefault(value => value.ItemId == source.SourceId);
            if (campaign.Launched?.ProgramId != programId || item is null || item.SystemInstanceId != source.RestrictedSystemInstanceId ||
                AccessReviewCampaignWork.WorkItemIdFor(tenantId, programId, campaignId, item.ItemId, source.Kind) != source.WorkItemId)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The original work differs from its actual frozen campaign/item/system source."));
        }
        if (source.AcknowledgementPersonId is { } personId)
            candidate = candidate with { AcknowledgementPerson = await acknowledgements.CaptureAsync(tenantId, personId, ct).ConfigureAwait(false) };
        return await queue.AuthorizeAssignmentReceiptAsync(tenantId, programId, actor, candidate, commandKind, ct).ConfigureAwait(false);
    }
}
