using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Runs one root native operation whose declared result is Unit on its first owner, with the handler order of the
///     design contract: resolve the retry from the owner's accepted facts, then (only for a new operation) the
///     feasibility checks that read other owners, the owner guard and the frozen first response, then the append.
///     An exact retry replays the original result even after the owner changed; a changed intent conflicts.
/// </summary>
public sealed class AdvisorUnitOperation(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content)
{
    /// <summary>Executes the operation and returns its unit result.</summary>
    /// <param name="context">The dispatch context.</param>
    /// <param name="tenantId">The realm of the frozen content.</param>
    /// <param name="provenance">The frozen provenance; the retry binding derives from it.</param>
    /// <param name="hydrate">Builds a fresh owner instance selected for the logical id.</param>
    /// <param name="acceptance">The owner's acceptance of the selected logical id.</param>
    /// <param name="feasibility">Checks of other owners for a new operation; null or success lets the guard decide.</param>
    /// <param name="guard">The owner guard, evaluated against the hydrated state when preparing and when appending.</param>
    /// <param name="apply">Raises the native fact with the frozen first response and operation result.</param>
    /// <param name="ct">The cancellation token.</param>
    public async ValueTask<Result> ExecuteAsync<TAggregate>(IExecutionContext context, Uuid tenantId,
        OperationProvenance provenance, Func<TAggregate> hydrate, Func<TAggregate, NativeAcceptance> acceptance,
        Func<TAggregate, CancellationToken, ValueTask<Result>>? feasibility, Func<TAggregate, Result> guard,
        Func<TAggregate, ImmutableContentRef, ImmutableContentRef, Result> apply, CancellationToken ct)
        where TAggregate : Aggregate
    {
        var binding = AcceptedOperationBinding.Of(provenance);
        var snapshot = await reader.HydrateAsync(hydrate(), ct).ConfigureAwait(false);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (acceptance(snapshot).Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (feasibility is not null)
            {
                var feasible = await feasibility(snapshot, ct).ConfigureAwait(false);
                if (!feasible.IsSuccess)
                    return feasible;
            }

            var guarded = guard(snapshot);
            if (!guarded.IsSuccess)
                return guarded;
            (response, result) = await AdvisorContent.FreezeUnitAsync(content, tenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<TAggregate>(binding, hydrate, acceptance,
            owner => AdvisorContent.Prepared(guard(owner), response),
            (owner, frozen) => apply(owner, frozen, result!)), context, ct).ConfigureAwait(false);
        return AdvisorContent.UnitOutcome(outcome);
    }
}
