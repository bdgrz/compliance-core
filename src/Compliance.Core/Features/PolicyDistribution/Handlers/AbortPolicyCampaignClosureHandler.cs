using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>
///     Cancels the exact pending closure and atomically fences every delayed local finalization of its generation. The
///     closure is found from the frozen intent on the authoritative campaign: a different revision, generation or
///     intent is a permanent conflict. A closure that already completed recovers forward and is never canceled; one
///     that was already canceled supersedes this abort with the original cancellation as evidence.
/// </summary>
sealed class AbortPolicyCampaignClosureHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, IDomainEventReader events) : IRequestHandler<AbortPolicyCampaignClosure, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AbortPolicyCampaignClosure> context,
        CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<ReceiverOutcome>> HandleCoreAsync(IRequestContext<AbortPolicyCampaignClosure> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.Generation < 1 ||
            command.ExpectedRevision < 1)
            return SourceUseReceiverSupport.Permanent("The abort names another generation, revision or operation.");
        if (!Uuid.TryParse(command.Target.Realm, System.Globalization.CultureInfo.InvariantCulture, out var tenantId) ||
            !Uuid.TryParse(command.Target.Resource, System.Globalization.CultureInfo.InvariantCulture, out var campaignId) ||
            command.Target.Area != PolicyDistributionCampaign.Area)
            return SourceUseReceiverSupport.Permanent("The abort names another campaign stream.");
        var campaign = await new PolicyCampaignReader(reader, events).ReadAsync(tenantId, campaignId,
            command.CommandId, selectedClosureRootId: command.OperationId, ct: ct).ConfigureAwait(false);
        if (campaign.Stream != command.Target || campaign.Owner.SelectedClosure is not { } accepted ||
            accepted.Intent != command.FailureProof || accepted.Revision != command.ExpectedRevision ||
            accepted.Generation != command.Generation)
            return SourceUseReceiverSupport.Permanent("An abort requires the exact frozen closure of its campaign.");
        var header = PolicyCampaignContent.HeaderOf(accepted.Operation, campaign.Stream, campaign.Stream,
            accepted.Operation.AcceptedAt);
        var provenance = PolicyCampaignContent.Child(command.CommandId, nameof(AbortPolicyCampaignClosure),
            accepted.Metadata.EventId, header, campaign.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var preparation = await NativeReceiverPreparation.PrepareAsync<PolicyDistributionCampaign, ClosureIntent>(reader,
            () => new PolicyDistributionCampaign(tenantId, campaignId, command.CommandId,
                selectedClosureRootId: command.OperationId), owner => Decide(owner, binding, command.OperationId),
            (_, token) => PrepareAsync(tenantId, campaignId, accepted, command, header, token), ct).ConfigureAwait(false);
        if (!preparation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(preparation.Error!);
        if (preparation.Value!.Outcome is { } known)
            return known;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new PolicyDistributionCampaign(tenantId, campaignId,
                    command.CommandId, selectedClosureRootId: command.OperationId), pending =>
                    {
                        if (Decide(pending, binding, command.OperationId) is { } decided)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(decided)));
                        var raised = pending.CancelClosure(provenance, command.FailureProof);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(SourceUseReceiverSupport.Applied(
                                NativeFactReference.Of(pending.Stream, pending.Acceptance.SelectedFact!)))))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Outcome;
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The campaign changed concurrently; rehydrate and decide again.
            }
        }
    }

    async ValueTask<Result<ClosureIntent>> PrepareAsync(Uuid tenantId, Uuid campaignId,
        PolicyCampaignClosureAccepted accepted, AbortPolicyCampaignClosure command, DurableIntentHeader header,
        CancellationToken ct)
    {
        ClosureIntent intent;
        try
        {
            intent = await PolicyCampaignContent.ReadAsync<ClosureIntent>(content, tenantId, command.FailureProof, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return Result<ClosureIntent>.Failure(SourceUseReceiverSupport.Permanent(
                $"The frozen closure cannot be verified: {exception.Message}").Error!);
        }
        return intent.Header != header || intent.ParentKind != ClosureParentKind.PolicyCampaign ||
            intent.ParentId != campaignId || intent.ParentGeneration != (ulong)accepted.Generation
            ? Result<ClosureIntent>.Failure(SourceUseReceiverSupport.Permanent(
                "The abort differs from the frozen closure intent.").Error!)
            : Result<ClosureIntent>.Success(intent);
    }

    static Result<ReceiverOutcome>? Decide(PolicyDistributionCampaign campaign, AcceptedOperationBinding binding,
        Uuid operationId)
    {
        switch (campaign.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(campaign.Stream, campaign.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }
        if (campaign.SelectedCancellation is { } canceled)
            return SourceUseReceiverSupport.Superseded("The closure was already canceled.",
                NativeFactReference.Of(campaign.Stream, canceled));
        return campaign.Closing is { } closed && closed.Operation.RootOperationId == operationId
            ? SourceUseReceiverSupport.Superseded("The closure already completed and recovers forward.",
                NativeFactReference.Of(campaign.Stream, closed))
            : null;
    }

    sealed record Step(Result<ReceiverOutcome> Outcome);
}
