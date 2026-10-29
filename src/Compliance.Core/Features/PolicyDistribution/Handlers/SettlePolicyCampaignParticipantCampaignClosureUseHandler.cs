using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>
///     The participant pair of the generic settle receiver. The terminal fact is resolved from the campaign owner: the
///     closed campaign requires <c>parent_closed</c> and installs its generation closure fence, a cancellation requires
///     <c>canceled</c>. A missing terminal fact is a transient wait, never an inferred cancellation, and elapsed time
///     never releases a use.
/// </summary>
sealed class SettlePolicyCampaignParticipantCampaignClosureUseHandler(IAggregateReader reader,
    IAggregateExecutor executor, IImmutableContentStore content, TimeProvider clock,
    PolicyCampaignClosureVerifier verifier)
    : SettleSourceUseHandler<PolicyCampaignParticipant, SettlePolicyCampaignParticipantCampaignClosureUse>(reader,
        executor, content, clock)
{
    protected override PolicyCampaignParticipant Open(SettlePolicyCampaignParticipantCampaignClosureUse request,
        Uuid selectedCommandId) => new(PolicyCampaignContent.ParseTenant(request.Target.Realm), request.CampaignId,
        request.PersonId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        SettlePolicyCampaignParticipantCampaignClosureUse request, CancellationToken ct)
    {
        var closure = await verifier.VerifyTerminalAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (closure.Error is not null)
            return Result<SourceUseSource>.Failure(closure.Error);
        var (campaign, intent) = (closure.Campaign!, closure.Intent!);
        var participant = closure.Participants!.SingleOrDefault(candidate =>
            candidate.Requirement.SourceOwner == request.Target);
        if (participant is null || participant.PersonId != request.PersonId || request.CampaignId != campaign.Id ||
            Uuid.Parse(participant.Target, System.Globalization.CultureInfo.InvariantCulture) != request.SourceOwnerId ||
            request.Generation != (long)intent.ParentGeneration)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The settlement differs from the frozen participant use requirement.", false));
        var disposition = campaign.Closing is { } closed && closed.Operation.RootOperationId == request.OperationId
            ? SourceUseSettlementDisposition.ParentClosed
            : SourceUseSettlementDisposition.Canceled;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            PolicyCampaignClosureEffects.UseKey(request.OperationId, intent.Header.FirstOwner, request.Generation,
                request.Target), participant.Requirement, nameof(SettlePolicyCampaignParticipantCampaignClosureUse),
            disposition, null));
    }

    protected override Func<PolicyCampaignParticipant, OperationProvenance, ImmutableContentRef, long, Result>
        SettleOperation() =>
        (PolicyCampaignParticipant participant, OperationProvenance provenance, ImmutableContentRef intent,
            long generation) => participant.SettleClosureUse(provenance, intent, generation);
}
