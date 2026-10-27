using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessReviews.Processes;

/// <summary>
///     Ends a pending completion that can definitely never close: the campaign cancellation is appended first, which
///     fences the closure and every delayed acquisition against late delivery, and only then are the item uses
///     settled (by the settlement reactor, even for uses that were never acquired, so a delayed acquisition is
///     refused). The abort uses the deterministic command id the process driver would use, so a later driver step
///     replays it. Nothing is inferred from missing native state.
/// </summary>
sealed class AccessReviewCompletionFences(IRequestBus bus, IImmutableContentStore content)
{
    public async ValueTask<Result<ReceiverOutcome>> AbortAsync(AccessReviewCompletion completion,
        NativeFactReference source, string reason, Uuid? itemId, IExecutionContext context, CancellationToken ct)
    {
        var (campaign, accepted, intent) = (completion.Campaign!, completion.Accepted!, completion.Intent!);
        var tenantId = Uuid.Parse(campaign.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var proof = await AccessReviewContent.WriteAsync(content, tenantId, AccessReviewContent.FailureProofSchema,
            new AccessReviewCompletionFailure(reason, itemId, NativeFactReference.Of(campaign.Stream, accepted)), ct)
            .ConfigureAwait(false);
        var abortId = AccessReviewEffects.ReceiverCommandId(AccessReviewEffects.CompletionProcessName, source.EventId,
            nameof(AbortAccessReviewCampaignCompletion), null, intent.Header.Episode);
        var aborted = await bus.SendAsync(new AbortAccessReviewCampaignCompletion(abortId,
            intent.Header.RootOperationId, campaign.Stream, AccessReviewEffects.Generation, accepted.Revision, proof),
            context, ct).ConfigureAwait(false);
        if (!aborted.IsSuccess)
            return aborted;
        // A completion that already closed recovers forward: its own settlement installs the closure fence.
        if (aborted.Value is ReceiverOutcome.Superseded)
            return aborted;
        var fact = aborted.Value switch
        {
            ReceiverOutcome.Applied applied => applied.Fact,
            ReceiverOutcome.Replayed replayed => replayed.Fact,
            _ => null,
        };
        return SourceUseReceiverSupport.Superseded(reason, fact);
    }
}
