using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     Materializes one admitted sample selection on its own owner at revision one, after the request closed if need be
///     and without reopening it. The frozen <see cref="AuditorSampleSelectionIntent" /> is confirmed on the authoritative
///     request: the complete nonempty duplicate-free row manifest and the population version content are verified, and
///     neither the live request nor the latest population is read. An exact replay returns the original fact.
/// </summary>
sealed class MaterializeAuditorSampleSelectionHandler(IAggregateReader reader, IAggregateExecutor executor, IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<MaterializeAuditorSampleSelection, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<MaterializeAuditorSampleSelection> context, CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.RequestId == Uuid.Empty ||
            command.SelectionId == Uuid.Empty || command.ExpectedRevision is not (null or 0) ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The materialization names another operation, selection or realm.");

        try
        {
            if (await AuditorChildMaterializationReplay.ResolveAsync(events, command.Source, command.Target, tenant,
                command.RequestId, command.SelectionId, command.CommandId, command.OperationId, command.Generation,
                command.Intent, AdmittedRequestChildKind.SampleSelection, ct).ConfigureAwait(false) is { } replay)
                return replay;
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact child materialization cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }

        AuditorSampleSelectionIntent intent;
        IReadOnlyList<Uuid> rows;
        AuditPopulations.Models.AuditPopulationVersionView population;
        try
        {
            intent = await AuditorContent.ReadAsync<AuditorSampleSelectionIntent>(content, tenant, command.Intent, ct)
                .ConfigureAwait(false);
            rows = await ImmutableSelectionManifests.ReadAsync<Uuid>(content, command.Target.Realm,
                intent.PopulationRowIds, ct).ConfigureAwait(false);
            population = await AuditorContent.ReadAsync<AuditPopulations.Models.AuditPopulationVersionView>(content, tenant,
                intent.PopulationContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return SourceUseReceiverSupport.Transient($"The frozen admission cannot be verified: {exception.Message}");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen admission cannot be verified: {exception.Message}");
        }

        var included = population.Rows.Where(static row => row.Classification == "included")
            .Select(static row => row.RowId).ToHashSet();
        if (population.Version != intent.PopulationVersion || !population.SampleEligible ||
            intent.RecordedAt != intent.Header.AcceptedAt || rows.Any(row => !included.Contains(row)) || rows.Count == 0 || rows.Distinct().Count() != rows.Count || rows.Any(static row => row == Uuid.Empty) ||
            intent.PopulationVersion <= 0)
            return SourceUseReceiverSupport.Permanent("The frozen selection is empty, duplicated or unversioned.");
        var programId = intent.ProgramId;
        var target = new AuditorSampleSelection(tenant, programId, command.RequestId, command.SelectionId).Stream;
        var parent = new AuditorRequest(tenant, programId, command.RequestId).Stream;
        if (intent.SelectionId != command.SelectionId || intent.RequestId != command.RequestId ||
            intent.Header.RootOperationId != command.OperationId || command.Target != target ||
            command.Source.Stream != parent || command.Generation != (long)intent.RequestGeneration)
            return SourceUseReceiverSupport.Permanent("The materialization differs from the frozen admission.");

        var admitted = await AdmittedChildVerifier.VerifyAsync<AuditorSampleSelectionAdmitted>(reader, events, content, tenant, programId,
            command.RequestId, command.Source, command.Intent, command.OperationId, command.SelectionId,
            fact => fact.Intent, ct).ConfigureAwait(false);
        if (!admitted.IsSuccess)
            return Result<ReceiverOutcome>.Failure(admitted.Error!);
        if (!AdmittedChildVerifier.HeaderMatches(admitted.Value!.Operation, intent.Header) ||
            admitted.Value.Generation != (long)intent.RequestGeneration || (ulong)admitted.Value.Revision != intent.ParentAdmissionRevision)
            return SourceUseReceiverSupport.Permanent("The frozen header differs from the exact native admission.");
        if (admitted.Value!.PopulationId != intent.PopulationId || admitted.Value.PopulationVersion != intent.PopulationVersion ||
            admitted.Value.SupersedesSelectionId != intent.SupersedesSelectionId)
            return SourceUseReceiverSupport.Permanent("The admitted population, version or lineage differs from the intent.");

        var provenance = AuditorRequestResolutionEffects.Child(command.CommandId,
            nameof(MaterializeAuditorSampleSelection), command.Source.EventId, intent.Header, target, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var result = await AuditorContent.UnitResultAsync(content, tenant, command.OperationId, ct).ConfigureAwait(false);
        var actor = Uuid.Parse(intent.Header.OriginalActor.Id, CultureInfo.InvariantCulture);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(Open(tenant, programId, command), ct).ConfigureAwait(false);
                if (Decide(current, binding) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(Open(tenant, programId, command), owner =>
                    {
                        if (Decide(owner, binding) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = owner.Materialize(provenance, command.Intent, actor, intent.RecordedAt, result);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(owner.Stream, owner.Recorded!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The selection changed concurrently; rehydrate and decide again.
            }
        }
    }

    static AuditorSampleSelection Open(Uuid tenant, Uuid program, MaterializeAuditorSampleSelection command) =>
        new(tenant, program, command.RequestId, command.SelectionId, command.CommandId);

    /// <summary>An exact replay of this receiver command, or the permanent conflict of a selection materialized otherwise.</summary>
    static Result<ReceiverOutcome>? Decide(AuditorSampleSelection owner, AcceptedOperationBinding binding)
    {
        switch (owner.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, owner.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return owner.IsMaterialized
            ? SourceUseReceiverSupport.Permanent("The sample selection was already materialized by another operation.")
            : null;
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
