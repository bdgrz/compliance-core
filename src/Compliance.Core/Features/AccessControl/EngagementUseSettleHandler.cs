using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The shared half of every settle pair of the engagement personal decision and acceptance processes. The terminal
///     fact is resolved from the engagement owner: an authorization or a recorded acceptance requires
///     <c>irreversible_decision</c>, a cancellation or a rejection requires <c>canceled</c>. A missing terminal fact is a
///     transient wait, never an inferred cancellation.
/// </summary>
/// <typeparam name="TOwner">The source owner that embeds <see cref="SourceUseReservations" />.</typeparam>
/// <typeparam name="TRequest">The pair's settle command.</typeparam>
abstract class EngagementUseSettleHandler<TOwner, TRequest>(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : SettleSourceUseHandler<TOwner, TRequest>(reader, executor, content, clock)
    where TOwner : Aggregate, ISourceUseOwner
    where TRequest : ISourceUseSettlement, IEngagementPersonalDecisionUse
{
    /// <summary>The command kind tagging the settlement fact.</summary>
    protected abstract string CommandKind { get; }

    protected override ValueTask<Result<SourceUseSource>> VerifyAsync(TRequest request, CancellationToken ct) =>
        verifier.SettleSourceAsync(request, CommandKind, ct);
}
