using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Materializes one selected occurrence. The frozen creation intent is read from verified content and the
///     selection that carries exactly that intent is found on the authoritative period; the plan approval it names is
///     found on the authoritative plan line. Nothing a command states is trusted until its owner confirms it. An exact
///     replay returns the original fact; a window already materialized by another generator with the same terms is
///     the same occurrence; different terms for one window are a permanent conflict. A fact that is not visible yet is
///     transient.
/// </summary>
sealed class MaterializeControlOccurrenceHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<MaterializeControlOccurrence, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<MaterializeControlOccurrence> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty ||
            command.Generation != ControlOccurrenceFanOut.Generation || command.ExpectedRevision is not (null or 0) ||
            !command.BusinessWindow.IsValid ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The materialization names another generation, operation or realm.");
        var realm = command.Target.Realm;

        ControlOccurrenceCreationIntent intent;
        try
        {
            intent = await content.ReadJsonAsync<ControlOccurrenceCreationIntent>(realm, command.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent($"The frozen creation intent cannot be verified: {exception.Message}");
        }

        var canonical = ControlOccurrenceIdentity.Of(tenant, command.ProgramId, command.ControlId, command.BusinessWindow);
        var target = new ControlOccurrence(tenant, command.ProgramId, command.ControlId, canonical).Stream;
        if (intent.TenantId != tenant || intent.ProgramId != command.ProgramId || intent.ControlId != command.ControlId ||
            intent.Window != command.BusinessWindow || !intent.Window.IsValid || intent.OccurrenceId != canonical ||
            command.OccurrenceId != canonical || command.Target != target ||
            intent.Header.RootOperationId != command.OperationId)
            return SourceUseReceiverSupport.Permanent("The materialization differs from the frozen creation intent.");
        if (intent.Window is not ControlCalendarWindow window)
            return SourceUseReceiverSupport.Permanent("Only a calendar window is selected by a recurring period.");

        var source = await VerifySourceAsync(tenant, command, ct).ConfigureAwait(false);
        if (source is { } rejected)
            return rejected;
        var planStream = new ControlOperatingPlan(tenant, command.ProgramId, command.ControlId).Stream;
        var plan = await reader.HydrateAsync(new ControlOperatingPlan(tenant, command.ProgramId, command.ControlId), ct)
            .ConfigureAwait(false);
        var line = await ControlOperatingPlanLine.ReadAsync(plan, content, tenant, ct).ConfigureAwait(false);
        if (line.Find(intent.PlanVersionId) is not { } version ||
            !line.Approvals.TryGetValue(intent.PlanVersionId, out var approval))
            return SourceUseReceiverSupport.Transient("The approved plan version is not visible yet.");
        if (intent.PlanProof.Stream != planStream || intent.PlanProof.EventId != approval.Metadata!.EventId ||
            intent.PlanProof.AggregateVersion != approval.Metadata.AggregateVersion ||
            version.Status is not (ControlOperationStates.Approved or ControlOperationStates.Superseded) ||
            version.Owner != intent.Owner || version.BackupOwner != intent.BackupOwner ||
            version.ReviewerMemberId != intent.ReviewerMemberId || version.ControlId != command.ControlId ||
            version.Cadence.Kind != ControlCadenceSchedule.Recurring || window.From < version.EffectiveFrom)
            return SourceUseReceiverSupport.Permanent("The creation intent differs from its approved plan version.");

        var now = clock.GetUtcNow();
        var periodEnd = window.UntilExclusive.AddDays(-1);
        var terms = new ControlOccurrenceTerms(ControlOperationStates.Expected, version.ControlVersionId,
            version.PlanVersionId, window.From, periodEnd, periodEnd.AddDays(version.Cadence.DueWithinDays ?? 0), null,
            (line.Current ?? version).Owner, intent.Header.OriginalActor, now, window);
        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(MaterializeControlOccurrence),
            command.Source.EventId, intent.Header, target, now);
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new ControlOccurrence(tenant, command.ProgramId,
                    command.ControlId, canonical, command.CommandId), ct).ConfigureAwait(false);
                if (Decide(current, binding) is { } early)
                    return early;
                if (current.Opened is { } existing)
                    return await SameTermsAsync(existing, intent, current.Stream, ct).ConfigureAwait(false);

                var step = await executor.ExecuteAsync(new ControlOccurrence(tenant, command.ProgramId,
                    command.ControlId, canonical, command.CommandId), occurrence =>
                    {
                        if (Decide(occurrence, binding) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        if (occurrence.Opened is not null)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Transient(
                                    "The occurrence was materialized concurrently; the replay is decided again."))));
                        var raised = occurrence.Materialize(provenance, terms, command.Intent);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(Fact(occurrence.Stream, occurrence.Opened!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The occurrence changed concurrently; rehydrate and decide again, including against another generator.
            }
        }
    }

    /// <summary>An exact replay of this receiver command, or its conflict with different accepted intent.</summary>
    static Result<ReceiverOutcome>? Decide(ControlOccurrence occurrence, AcceptedOperationBinding binding) =>
        occurrence.Acceptance.Decide(binding) switch
        {
            NativeAcceptanceDecision.Replayed => SourceUseReceiverSupport.Replayed(Fact(occurrence.Stream,
                occurrence.Acceptance.SelectedFact!)),
            NativeAcceptanceDecision.Conflict => Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError),
            _ => null,
        };

    /// <summary>The selection that carries exactly this intent must be found on the authoritative period.</summary>
    async ValueTask<Result<ReceiverOutcome>?> VerifySourceAsync(Uuid tenant, MaterializeControlOccurrence command,
        CancellationToken ct)
    {
        var source = command.Source;
        if (source.Stream.Realm != command.Target.Realm || source.Stream.Area != TypeIIPeriod.Area ||
            !Uuid.TryParse(source.Stream.Resource, CultureInfo.InvariantCulture, out var periodId))
            return SourceUseReceiverSupport.Permanent("The source is not a Type II period selection.");
        var period = await reader.HydrateAsync(new TypeIIPeriod(tenant, command.ProgramId, periodId, selectedEventId: source.EventId), ct)
            .ConfigureAwait(false);
        if (period.FactById(source.EventId) is not DueControlOccurrencesGenerated selected)
            return SourceUseReceiverSupport.Transient("The occurrence selection is not visible on its period yet.");
        if (selected.Metadata!.AggregateVersion != source.AggregateVersion ||
            selected.Operation.RootOperationId != command.OperationId)
            return SourceUseReceiverSupport.Permanent("The source differs from the selection of this operation.");
        try
        {
            var manifest = await content.ReadJsonAsync<ControlOccurrenceSelectionManifest>(command.Target.Realm,
                selected.CreationIntentManifest, ct).ConfigureAwait(false);
            return manifest.Selections.Any(selection => selection.OccurrenceId == command.OccurrenceId &&
                                                        selection.Intent == command.Intent)
                ? null
                : SourceUseReceiverSupport.Permanent("The selection does not carry this occurrence and intent.");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Transient($"The selection manifest cannot be verified yet: {exception.Message}");
        }
    }

    async Task<Result<ReceiverOutcome>> SameTermsAsync(ControlOccurrenceOpened existing,
        ControlOccurrenceCreationIntent intent, EventStreamAddress stream, CancellationToken ct)
    {
        try
        {
            var materialized = await content.ReadJsonAsync<ControlOccurrenceCreationIntent>(stream.Realm,
                existing.CreationIntent, ct).ConfigureAwait(false);
            return materialized.SameTermsAs(intent)
                ? SourceUseReceiverSupport.Replayed(Fact(stream, existing))
                : SourceUseReceiverSupport.Permanent(
                    "The business window is already materialized with different frozen terms.");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return SourceUseReceiverSupport.Permanent(
                $"The materialized creation intent cannot be verified: {exception.Message}");
        }
    }

    static NativeFactReference Fact(EventStreamAddress stream, DomainEvent fact) => NativeFactReference.Of(stream, fact);

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
