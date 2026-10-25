using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The shared flow of the two completion receivers. The source must be the irreversible authorization, every
///     practice subject must hold its exact permanent commitment, and only then is the recorded fact appended with the
///     snapshot derived from the frozen evidence at the frozen decision instant. After the authorization this always
///     finishes forward: there is no path that skips or reverses it.
/// </summary>
sealed class EngagementCompletion(IAggregateReader reader, IAggregateExecutor executor, IImmutableContentStore content,
    TimeProvider clock, EngagementAcceptanceVerifier verifier)
{
    const int MaximumAttempts = 4;

    /// <summary>Loads the authorized operation of a completion command and proves its permanent commitments.</summary>
    public async ValueTask<LoadedOperation> PrepareAsync(NativeFactReference source,
        ImmutableContentRef intent, Uuid operationId, Uuid commandId, EventStreamAddress target, long generation,
        Uuid engagementId, long serviceEpoch, ImmutableContentRef commitmentManifest,
        EngagementIndependenceOperationKind kind, CancellationToken ct)
    {
        if (commandId == Uuid.Empty || operationId == Uuid.Empty || generation != EngagementAcceptanceEffects.Generation)
            return LoadedOperation.Failed(EngagementAcceptanceVerifier.Permanent(
                "The completion names another operation or generation."));
        var loaded = await verifier.LoadAsync(source, intent, operationId, engagementId, commandId, ct,
            OperationSource.Authorization).ConfigureAwait(false);
        if (loaded.Error is not null)
            return loaded;
        if (target != loaded.Owner!.Stream || loaded.Intent!.OperationKind != kind ||
            loaded.Intent.ServiceEpoch != (ulong)serviceEpoch)
            return LoadedOperation.Failed(EngagementAcceptanceVerifier.Permanent(
                "The completion differs from the frozen operation."));
        var tenantId = EngagementIndependenceIntent.TenantOf(loaded.Intent.Header);
        NativeFactReference[] commitments;
        try
        {
            commitments = await content.ReadJsonAsync<NativeFactReference[]>(IndependenceContent.Realm(tenantId),
                commitmentManifest, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return LoadedOperation.Failed(EngagementAcceptanceVerifier.Permanent(
                $"The commitment proofs cannot be verified: {exception.Message}"));
        }

        if (commitments.Length != loaded.Subjects!.Count)
            return LoadedOperation.Failed(EngagementAcceptanceVerifier.Permanent(
                "The proofs do not cover exactly the frozen practice subjects."));
        foreach (var subject in loaded.Subjects)
        {
            var key = EngagementAcceptanceEffects.UseKey(operationId, loaded.Owner.Stream, subject.Stream);
            var owner = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId, subject.Kind,
                subject.SubjectId, selectedReservation: key), ct).ConfigureAwait(false);
            if (owner.CommitmentOf(key) is not { } committed)
                return LoadedOperation.Failed(EngagementAcceptanceVerifier.Transient(
                    "A permanent commitment is not visible yet; completion waits for the complete set."));
            if (!commitments.Contains(NativeFactReference.Of(owner.Stream, committed)))
                return LoadedOperation.Failed(EngagementAcceptanceVerifier.Permanent(
                    "The proofs do not name the exact permanent commitment of a practice subject."));
        }

        return loaded;
    }

    /// <summary>
    ///     Appends the completion once. <paramref name="record" /> raises the recorded fact on the fresh owner; a
    ///     replay of the exact command returns the original fact.
    /// </summary>
    public async ValueTask<Result<ReceiverOutcome>> AppendAsync(LoadedOperation loaded, Uuid commandId,
        string commandKind, NativeFactReference source, IExecutionContext context,
        Func<ServiceEngagementIndependence, OperationProvenance, Result> record, CancellationToken ct)
    {
        var (owner, operation, intent) = (loaded.Owner!, loaded.Operation!, loaded.Intent!);
        var provenance = EngagementAcceptanceEffects.Child(commandId, commandKind, source.EventId, intent,
            owner.Stream, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var operationId = operation.RequestedOperation.Operation.RootOperationId;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var step = await executor.ExecuteAsync(new ServiceEngagementIndependence(owner.TenantId,
                    owner.EngagementId, commandId), fresh =>
                    {
                        switch (fresh.Acceptance.Decide(binding))
                        {
                            case NativeAcceptanceDecision.Replayed:
                                return AggregateOutcome.Discard(Result<Step>.Success(
                                    new Step(NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), false)));
                            case NativeAcceptanceDecision.Conflict:
                                return AggregateOutcome.Discard(Result<Step>.Failure(AcceptedAppendResult.ConflictError));
                        }

                        if (fresh.OperationOf(operationId) is { Completed: { } existing })
                            return AggregateOutcome.Discard(Result<Step>.Success(
                                new Step(NativeFactReference.Of(fresh.Stream, existing), false)));
                        var recorded = record(fresh, provenance);
                        return recorded.IsSuccess
                            ? AggregateOutcome.Commit(Result<Step>.Success(new Step(
                                NativeFactReference.Of(fresh.Stream, fresh.Acceptance.SelectedFact!), true)))
                            : AggregateOutcome.Discard(Result<Step>.Failure(recorded.Error!));
                    }, context, ct).ConfigureAwait(false);
                if (!step.IsSuccess)
                    return Result<ReceiverOutcome>.Failure(step.Error!);
                return step.Value!.Applied
                    ? SourceUseReceiverSupport.Applied(step.Value.Fact)
                    : SourceUseReceiverSupport.Replayed(step.Value.Fact);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // A concurrent delivery won the append; rehydrate and decide again.
            }
        }
    }

    /// <summary>Reads the current accepted snapshot of an engagement owner, or null before its acceptance.</summary>
    public async Task<ServiceEngagementAcceptanceView?> ReadAcceptanceAsync(ServiceEngagementIndependence owner,
        CancellationToken ct) => owner.AcceptanceResult is { } result
        ? await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content,
            IndependenceContent.Realm(owner.TenantId), result, ct).ConfigureAwait(false)
        : null;

    /// <summary>Reads the frozen evidence of an operation.</summary>
    public Task<EngagementAcceptanceEvidence> ReadEvidenceAsync(EngagementIndependenceIntent intent, CancellationToken ct) =>
        content.ReadJsonAsync<EngagementAcceptanceEvidence>(
            IndependenceContent.Realm(EngagementIndependenceIntent.TenantOf(intent.Header)), intent.Evidence, ct);

    /// <summary>Writes a deterministic content item so a retried completion writes the identical bytes.</summary>
    public Task<ImmutableContentRef> WriteAsync<T>(Uuid tenantId, Uuid operationId, string schema, T value,
        CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(IndependenceContent.Realm(tenantId), Uuid.CreateVersion5(operationId, schema), schema,
            value, ct);

    /// <summary>Writes the terminal result wrapper around a view content item.</summary>
    public Task<ImmutableContentRef> WriteResultAsync(Uuid tenantId, Uuid operationId, ImmutableContentRef view,
        CancellationToken ct) =>
        content.WriteJsonAsync(IndependenceContent.Realm(tenantId),
            Uuid.CreateVersion5(operationId, IndependenceContent.ResultSchema + ":" + EngagementAcceptanceEffects.ResultSchema),
            IndependenceContent.ResultSchema,
            new MutationResultContent(EngagementAcceptanceEffects.ResultSchema, new StandardResponseBody(view)), ct);

    sealed record Step(NativeFactReference Fact, bool Applied);
}
