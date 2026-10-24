using System.Globalization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Commits the permanent practice wall of one reserved subject. The source is verified to be the irreversible
///     authorization of the engagement owner, so no commitment is ever made while any subject reservation can still
///     fail, and the exact active reservation of the operation must be held. The commitment survives every later
///     outcome: nothing in this owner removes it.
/// </summary>
sealed class CommitPracticeIndependenceHandler(IAggregateExecutor executor, TimeProvider clock,
    EngagementAcceptanceVerifier verifier)
    : IRequestHandler<CommitPracticeIndependence, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CommitPracticeIndependence> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != EngagementAcceptanceEffects.Generation)
            return SourceUseReceiverSupport.Permanent("The commitment names another operation or generation.");
        var loaded = await verifier.LoadAsync(command.Source, command.Intent, command.OperationId, Uuid.Empty,
            command.CommandId, ct, OperationSource.Authorization).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<ReceiverOutcome>.Failure(loaded.Error);
        var (owner, intent) = (loaded.Owner!, loaded.Intent!);
        var subject = loaded.Subjects!.SingleOrDefault(item => item.Stream == command.Target);
        if (subject is null || subject.Kind != command.SubjectKind || subject.SubjectId != command.SubjectId ||
            subject.Practice != command.Practice)
            return SourceUseReceiverSupport.Permanent("The commitment differs from the frozen practice subject.");

        var tenantId = EngagementIndependenceIntent.TenantOf(intent.Header);
        var engagementId = Uuid.Parse(owner.Stream.Resource, CultureInfo.InvariantCulture);
        var provenance = EngagementAcceptanceEffects.Child(command.CommandId, nameof(CommitPracticeIndependence),
            command.Source.EventId, intent, command.Target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new PracticeIndependenceCommitment(tenantId, subject.Kind,
                    subject.SubjectId, command.CommandId), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.CommitmentOf(command.OperationId) is { } existing)
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                new Step(NativeFactReference.Of(fresh.Stream, existing), false)));
                        var committed = fresh.Commit(provenance, command.Intent, command.Generation, command.Practice,
                            engagementId);
                        return committed.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), true)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(committed.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The owner changed concurrently; rehydrate and decide again.
            }
        }
    }

    sealed record Step(NativeFactReference Fact, bool Applied);
}
