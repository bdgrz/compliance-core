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
public sealed class AdvisorUnitOperation(NativeOperationExecutor operations, IImmutableContentStore content)
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
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<TAggregate>(binding, hydrate, acceptance,
            async (owner, token) =>
            {
                if (feasibility is not null)
                {
                    var feasible = await feasibility(owner, token).ConfigureAwait(false);
                    if (!feasible.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(feasible.Error!);
                }
                var guarded = guard(owner);
                if (!guarded.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(guarded.Error!);
                var frozen = await AdvisorContent.FreezeUnitAsync(content, tenantId, token).ConfigureAwait(false);
                result = frozen.Result;
                return Result<FirstResponseDescriptor>.Success(frozen.Response);
            }, (owner, frozen) => apply(owner, frozen, result!)), context, ct).ConfigureAwait(false);
        return AdvisorContent.UnitOutcome(outcome);
    }
}
