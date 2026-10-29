using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.AccessReviews.Services;

/// <summary>What the cancellation and settlement reactors of a batch read before they dispatch a receiver.</summary>
static class AccessReviewBulkReactions
{
    /// <summary>
    ///     The tenant, the frozen intent and the admission fact a batch fact derives from. A fact that is not visible
    ///     yet is transient; the receivers verify the same admission, so nothing here is trusted on its own.
    /// </summary>
    public static async Task<(Uuid Tenant, BulkAccessDecisionIntent Intent, NativeFactReference Source)> ResolveAsync(
        string realm, ImmutableContentRef intentReference, IImmutableContentStore content, IAggregateReader reader,
        CancellationToken ct)
    {
        var tenant = Uuid.Parse(realm, CultureInfo.InvariantCulture);
        BulkAccessDecisionIntent intent;
        try
        {
            intent = await AccessReviewContent.ReadAsync<BulkAccessDecisionIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen bulk decision cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        var campaign = await reader.HydrateAsync(new AccessReviewCampaign(tenant, intent.CampaignId, selectedRootId: intent.DecisionBatchId), ct)
            .ConfigureAwait(false);
        if (campaign.AdmittedFor(intent.DecisionBatchId) is not { } admitted)
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                "The admitted bulk decision is not visible yet.", true));
        return (tenant, intent, BulkReceiverSupport.Admission(campaign, admitted));
    }

    /// <summary>The selected items of a batch in canonical key order.</summary>
    public static async Task<IReadOnlyList<BulkAccessDecisionItemIntent>> ItemsAsync(AccessReviewManifests manifests,
        string realm, BulkAccessDecisionIntent intent, CancellationToken ct)
    {
        try
        {
            return [.. (await manifests.BulkItemsAsync(realm, intent.Items, ct).ConfigureAwait(false)).Values
                .OrderBy(static item => item.ItemOwner.Resource, Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance)];
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen bulk decision cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
    }
}
