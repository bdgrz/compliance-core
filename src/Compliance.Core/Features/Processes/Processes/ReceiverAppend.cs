using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>
///     Accepts one fact of a process receiver on its target owner. A replay returns the original fact, a conflicting
///     reuse of the logical id fails permanently, <c>early</c> may stop with a wait, a rejection or a supersession,
///     and a lost append race is retried against the rehydrated owner. The owner is always opened for the receiver's
///     own logical id, so the decision is by logical id and never by current state.
/// </summary>
public sealed class ReceiverAppend(IAggregateExecutor executor)
{
    const int MaximumAttempts = 5;

    /// <summary>The transient error for a target that changed while the receiver applied.</summary>
    public static Result<ReceiverOutcome> Wait(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, true));

    /// <summary>The permanent error for a proof, intent or command that does not match the admission.</summary>
    public static Result<ReceiverOutcome> Invalid(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, false));

    /// <summary>A terminal no-op because a proven later episode or generation replaced this step.</summary>
    public static Result<ReceiverOutcome> Superseded(string reason) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Superseded(reason, null));

    /// <summary>A definite business refusal that aborts the process.</summary>
    public static Result<ReceiverOutcome> Rejected(string reason) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(reason));

    /// <summary>Appends the receiver's fact, or returns the replay, wait, rejection or supersession that applies.</summary>
    public async ValueTask<Result<ReceiverOutcome>> ApplyAsync<TAggregate>(Func<TAggregate> create,
        Func<TAggregate, NativeAcceptance> acceptance, AcceptedOperationBinding binding,
        Func<TAggregate, Result<ReceiverOutcome>?> early, Func<TAggregate, Result> operate, IExecutionContext context,
        CancellationToken ct) where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(acceptance);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(early);
        ArgumentNullException.ThrowIfNull(operate);
        ArgumentNullException.ThrowIfNull(context);
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await executor.ExecuteAsync<TAggregate, ReceiverOutcome>(create(), aggregate =>
                {
                    var accepted = acceptance(aggregate);
                    switch (accepted.Decide(binding))
                    {
                        case NativeAcceptanceDecision.Replayed:
                            return AggregateOutcome.Discard(Result<ReceiverOutcome>.Success(
                                new ReceiverOutcome.Replayed(NativeFactReference.Of(aggregate.Stream,
                                    accepted.SelectedFact!))));
                        case NativeAcceptanceDecision.Conflict:
                            return AggregateOutcome.Discard(Result<ReceiverOutcome>.Failure(
                                AcceptedAppendResult.ConflictError));
                    }

                    if (early(aggregate) is { } stop)
                        return AggregateOutcome.Discard(stop);
                    var applied = operate(aggregate);
                    if (!applied.IsSuccess)
                        return AggregateOutcome.Discard(Result<ReceiverOutcome>.Failure(applied.Error!));
                    return AggregateOutcome.Commit(Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(
                        NativeFactReference.Of(aggregate.Stream, accepted.SelectedFact
                            ?? throw new InvalidOperationException("The receiver raised no fact for its logical id.")))));
                }, context, ct).ConfigureAwait(false);
            }
            catch (EventStreamConcurrencyException) when (attempt < MaximumAttempts)
            {
                // The append lost the owner's OCC: rehydrate and decide again by logical id.
            }
            catch (EventStreamConcurrencyException)
            {
                return Wait("The target changed while the receiver applied; it will be retried.");
            }
        }
    }
}
