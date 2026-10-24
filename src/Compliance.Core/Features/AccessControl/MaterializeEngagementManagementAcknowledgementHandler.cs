using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Materializes the immutable management acknowledgement of an authorized personal decision. It consumes the
///     authorization fact and the frozen intent only: the acknowledgement keeps the original authenticated actor and
///     the frozen decision instant, never the receiver's clock, and an exact replay returns the original fact.
/// </summary>
sealed class MaterializeEngagementManagementAcknowledgementHandler(IAggregateExecutor executor,
    IImmutableContentStore content, EngagementPersonalDecisionVerifier verifier)
    : IRequestHandler<MaterializeEngagementManagementAcknowledgement, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<MaterializeEngagementManagementAcknowledgement> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.AcknowledgementId == Uuid.Empty ||
            command.OperationId != command.AcknowledgementId ||
            command.Generation != EngagementPersonalDecisionEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The acknowledgement names another operation or generation.");
        if (!Uuid.TryParse(command.Source.Stream.Resource, out var engagementId))
            return SourceUseReceiverSupport.Permanent("The source is not an engagement fact.");
        var loaded = await verifier.LoadAsync(command.Source, command.Intent, command.OperationId,
            engagementId, command.CommandId, ct, DecisionSource.Authorization).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (engagement, decision, intent) = (loaded.Owner!, loaded.Decision!, loaded.Intent!);
        var tenantId = engagement.TenantId;
        var target = new EngagementManagementAcknowledgement(tenantId, command.AcknowledgementId).Stream;
        if (command.Target != target || intent.Kind != EngagementPersonalDecisionKind.ManagementAcknowledgement ||
            intent.AuthoredContent is not ManagementAcknowledgementContent authored)
            return SourceUseReceiverSupport.Permanent("The command differs from the frozen personal decision intent.");
        var authorized = decision.Authorized!;

        var realm = IndependenceContent.Realm(tenantId);
        IReadOnlyList<Uuid> serviceIds;
        try
        {
            serviceIds = await content.ReadSelectionAsync<Uuid>(realm, authored.CompleteServiceRecordIds, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen acknowledgement cannot be verified: {exception.Message}");
        }

        var view = new EngagementManagementAcknowledgementView(tenantId, intent.EngagementId,
            command.AcknowledgementId, (long)intent.ExpectedEngagementRevision, serviceIds, authored.Statement,
            authored.UserId, intent.Header.OriginalActor, authorized.DecisionAt);
        var acknowledgement = await IndependenceContent.WriteAsync(content, realm,
            EngagementPersonalDecisionEffects.AcknowledgementSchema, view, ct).ConfigureAwait(false);
        var result = await IndependenceContent.WriteResultAsync(content, realm,
            EngagementPersonalDecisionEffects.AcknowledgementSchema, acknowledgement, ct).ConfigureAwait(false);

        var provenance = EngagementPersonalDecisionEffects.Child(command.CommandId,
            nameof(MaterializeEngagementManagementAcknowledgement), command.Source.EventId, intent, target,
            authorized.DecisionAt);
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new EngagementManagementAcknowledgement(tenantId,
                    command.AcknowledgementId, command.CommandId), child =>
                    {
                        switch (child.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(child.Stream, child.Acceptance.SelectedFact!), false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (child.Acknowledged is { } existing)
                            return AggregateOutcome.Discard(existing.Intent == command.Intent
                                ? Result<Step>.Success(new Step(NativeFactReference.Of(child.Stream, existing), false))
                                : Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        var materialized = child.Materialize(provenance, acknowledgement, command.Intent, result);
                        return materialized.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(child.Stream, child.Acceptance.SelectedFact!), true)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(materialized.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent delivery of the same acknowledgement won the append; rehydrate and decide again.
            }
        }
    }

    sealed record Step(NativeFactReference Fact, bool Applied);
}
