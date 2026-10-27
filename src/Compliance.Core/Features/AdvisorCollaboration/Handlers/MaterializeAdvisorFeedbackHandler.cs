using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;

/// <summary>
///     Materializes one admitted feedback contribution on its own thread at revision one. The frozen admission intent
///     is read from verified content and the admission that carries exactly that intent is found on the authoritative
///     handoff; nothing a command states is trusted until its owner confirms it. An exact replay returns the original
///     fact. An admission that is not visible yet is transient, a changed intent is a permanent conflict, and the
///     contribution is the admitted terms with the original actor and the admission instant, so the handoff is never
///     loaded again and a revoked or closed handoff still materializes what it admitted.
/// </summary>
public sealed class MaterializeAdvisorFeedbackHandler(IAggregateReader reader, IAggregateExecutor executor,
    AdvisorHandoffReader handoffs, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<MaterializeAdvisorFeedback, ReceiverOutcome>
{
    const int MaximumAttempts = 4;

    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<MaterializeAdvisorFeedback> context,
        CancellationToken ct) => AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<ReceiverOutcome>> HandleCoreAsync(IRequestContext<MaterializeAdvisorFeedback> context,
        CancellationToken ct)
    {
        var command = context.Request;
        if (command.CommandId == Uuid.Empty || command.OperationId == Uuid.Empty || command.FeedbackId == Uuid.Empty ||
            command.HandoffId == Uuid.Empty || command.ExpectedRevision is not (null or 0) ||
            !Uuid.TryParse(command.Target.Realm, CultureInfo.InvariantCulture, out var tenant))
            return SourceUseReceiverSupport.Permanent("The materialization names another operation, feedback or realm.");

        ChildAdmissionIntent intent;
        RecordAdvisorFeedback authored;
        try
        {
            intent = await AdvisorContent.ReadAsync<ChildAdmissionIntent>(content, tenant, command.Intent, ct)
                .ConfigureAwait(false);
            authored = await AdvisorContent.ReadAsync<RecordAdvisorFeedback>(content, tenant,
                intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            ct.ThrowIfCancellationRequested();
            return SourceUseReceiverSupport.Transient($"The frozen admission cannot be verified: {exception.Message}");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            ct.ThrowIfCancellationRequested();
            return SourceUseReceiverSupport.Permanent($"The frozen admission cannot be verified: {exception.Message}");
        }

        var target = new AdvisorFeedbackThread(tenant, authored.ProgramId, command.HandoffId, command.FeedbackId).Stream;
        var handoff = new AdvisorHandoff(tenant, authored.ProgramId, command.HandoffId).Stream;
        if (intent.ChildKind != AdmittedChildKind.AdvisorFeedback || intent.ChildId != command.FeedbackId ||
            intent.ParentId != command.HandoffId || intent.Header.RootOperationId != command.OperationId ||
            authored.FeedbackId != command.FeedbackId || authored.TenantId != tenant ||
            authored.HandoffId != command.HandoffId || command.Target != target || command.Source.Stream != handoff ||
            command.Generation != (long)intent.ParentGeneration)
            return SourceUseReceiverSupport.Permanent("The materialization differs from the frozen admission.");

        var parent = await reader.HydrateAsync(new AdvisorHandoff(tenant, authored.ProgramId, command.HandoffId), ct)
            .ConfigureAwait(false);
        if (await handoffs.FindFactAsync(parent, command.Source.EventId, ct).ConfigureAwait(false) is not AdvisorFeedbackAdmitted admitted)
            return SourceUseReceiverSupport.Transient("The feedback admission is not visible on its handoff yet.");
        if (admitted.Metadata.AggregateVersion != command.Source.AggregateVersion || admitted.Intent != command.Intent ||
            admitted.Operation.LogicalId != command.FeedbackId || admitted.Operation.RootOperationId != command.OperationId ||
            intent.Header != AdvisorContent.Header(admitted.Operation, admitted.Operation.AcceptedAt) ||
            intent.ParentGeneration != (ulong)admitted.Generation || intent.ParentAdmissionRevision != (ulong)admitted.Revision)
            return SourceUseReceiverSupport.Permanent("The source differs from the admission of this feedback.");

        var provenance = SourceUseReceiverSupport.Child(command.CommandId, nameof(MaterializeAdvisorFeedback),
            command.Source.EventId, intent.Header, target, clock.GetUtcNow());
        _ = await AdvisorFeedbackReader.ReadRecordedAsync(content, new AdvisorFeedbackRecorded(tenant,
            authored.ProgramId, command.HandoffId, command.FeedbackId, authored.RecordId, authored.RecordVersionId,
            authored.Kind, intent.Header.OriginalActor, intent.Header.AcceptedAt, provenance, command.Intent, null, null)
        { SelectedSource = authored.SelectedSource }, ct).ConfigureAwait(false);
        var binding = AcceptedOperationBinding.Of(provenance);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var current = await reader.HydrateAsync(new AdvisorFeedbackThread(tenant, authored.ProgramId,
                    command.HandoffId, command.FeedbackId, command.CommandId), ct).ConfigureAwait(false);
                if (Decide(current, binding) is { } early)
                    return early;
                var step = await executor.ExecuteAsync(new AdvisorFeedbackThread(tenant, authored.ProgramId,
                    command.HandoffId, command.FeedbackId, command.CommandId), thread =>
                    {
                        if (Decide(thread, binding) is { } decided)
                            return AggregateOutcome.Discard(Result<Decided>.Success(new Decided(decided)));
                        var raised = thread.Materialize(provenance, authored, intent.Header.OriginalActor,
                            intent.Header.AcceptedAt, command.Intent);
                        return raised.IsSuccess
                            ? AggregateOutcome.Commit(Result<Decided>.Success(new Decided(
                                SourceUseReceiverSupport.Applied(NativeFactReference.Of(thread.Stream, thread.Recorded!)))))
                            : AggregateOutcome.Discard(Result<Decided>.Failure(raised.Error!));
                    }, context, ct).ConfigureAwait(false);
                return step.IsSuccess ? step.Value!.Outcome : Result<ReceiverOutcome>.Failure(step.Error!);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The thread changed concurrently; rehydrate and decide again.
            }
        }
    }

    /// <summary>An exact replay of this receiver command, or the permanent conflict of a thread materialized otherwise.</summary>
    static Result<ReceiverOutcome>? Decide(AdvisorFeedbackThread thread, AcceptedOperationBinding binding)
    {
        switch (thread.Acceptance.Decide(binding))
        {
            case NativeAcceptanceDecision.Replayed:
                return SourceUseReceiverSupport.Replayed(NativeFactReference.Of(thread.Stream, thread.Acceptance.SelectedFact!));
            case NativeAcceptanceDecision.Conflict:
                return Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError);
        }

        return thread.IsMaterialized
            ? SourceUseReceiverSupport.Permanent("The advisor feedback was already materialized by another operation.")
            : null;
    }

    sealed record Decided(Result<ReceiverOutcome> Outcome);
}
