using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Cancels the exact pending closure and atomically fences every delayed local finalization of its generation. The
///     closure is found from the frozen intent on the authoritative campaign: a different revision, generation or
///     intent is a permanent conflict. A closure that already completed recovers forward and is never canceled; one
///     that was already canceled supersedes this abort with the original cancellation as evidence.
/// </summary>
sealed class AbortPolicyCampaignClosureHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<AbortPolicyCampaignClosure, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AbortPolicyCampaignClosure> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.Generation < 1 ||
            command.ExpectedRevision < 1)
            return SourceUseReceiverSupport.Permanent("The abort names another generation, revision or operation.");
        var tenantId = PolicyCampaignContent.ParseTenant(command.Target.Realm);
        ClosureIntent intent;
        try
        {
            intent = await PolicyCampaignContent.ReadAsync<ClosureIntent>(content, tenantId, command.FailureProof, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen closure cannot be verified: {exception.Message}");
        }

        if (intent.Header.RootOperationId != command.OperationId || intent.ParentKind != ClosureParentKind.PolicyCampaign ||
            intent.Header.FirstOwner != command.Target)
            return SourceUseReceiverSupport.Permanent("The abort differs from the frozen closure intent.");
        var campaign = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId, intent.ParentId,
            command.CommandId), ct).ConfigureAwait(false);
        if (campaign.Stream != command.Target || campaign.ClosureFor(command.OperationId) is not { } accepted ||
            accepted.Intent != command.FailureProof || accepted.Revision != command.ExpectedRevision ||
            accepted.Generation != command.Generation)
            return SourceUseReceiverSupport.Permanent("An abort requires the exact frozen closure of its campaign.");

        var provenance = PolicyCampaignContent.Child(command.CommandId, nameof(AbortPolicyCampaignClosure),
            accepted.Metadata.EventId, intent.Header, campaign.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new PolicyDistributionCampaign(tenantId, intent.ParentId,
                    command.CommandId), pending =>
                    {
                        switch (pending.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(new Step(
                                    NativeFactReference.Of(pending.Stream, pending.Acceptance.SelectedFact!), false,
                                    false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (pending.CanceledFor(command.OperationId) is { } canceled)
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                new Step(NativeFactReference.Of(pending.Stream, canceled), false, true)));
                        if (pending.Closing is { } closed && closed.Operation.RootOperationId == command.OperationId)
                            return AggregateOutcome.Discard(Result<Step>.Success(new Step(null, false, true)));
                        var raised = pending.CancelClosure(provenance, command.FailureProof);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(pending.Stream, pending.Acceptance.SelectedFact!), true,
                                false)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                if (step.Value!.Foreign)
                    return SourceUseReceiverSupport.Superseded(step.Value.Fact is null
                        ? "The closure already completed and recovers forward."
                        : "The closure was already canceled.", step.Value.Fact);
                return step.Value.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact!)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The campaign changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>Fact is the appended, replayed or evidencing fact; Foreign marks the superseded branch.</summary>
    sealed record Step(NativeFactReference? Fact, bool Applied, bool Foreign);
}
