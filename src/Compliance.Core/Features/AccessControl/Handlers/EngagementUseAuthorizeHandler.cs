using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The shared half of every acquire pair of the engagement personal decision and acceptance processes: the source
///     is verified from the engagement owner, and a pinned source that is no longer eligible rejects the whole process
///     so the abort receiver fences authorization and every use, held or not, is settled as canceled.
/// </summary>
/// <typeparam name="TOwner">The source owner that embeds <see cref="SourceUseReservations" />.</typeparam>
/// <typeparam name="TRequest">The pair's authorize command.</typeparam>
abstract class EngagementUseAuthorizeHandler<TOwner, TRequest>(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementUseVerifier verifier)
    : AuthorizeSourceUseHandler<TOwner, TRequest>(reader, executor, content, clock)
    where TOwner : Aggregate, ISourceUseOwner
    where TRequest : ISourceUseAuthorization, IEngagementPersonalDecisionUse
{
    /// <summary>The command kind tagging the grant fact.</summary>
    protected abstract string CommandKind { get; }

    protected override ValueTask<Result<SourceUseSource>> VerifyAsync(TRequest request, CancellationToken ct) =>
        verifier.AuthorizeSourceAsync(request, CommandKind, ct);

    protected override ValueTask<Result<ReceiverOutcome>> RefuseAsync(TRequest request, SourceUseSource source,
        string reason, IExecutionContext context, CancellationToken ct) => ValueTask.FromResult(
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Rejected(reason, null)));

    /// <summary>A pinned source that is not eligible any more fails the whole decision permanently.</summary>
    protected static Result Stale(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message,
        false));

    /// <summary>A pinned source that is not visible yet is waited for.</summary>
    protected static Result Waiting(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        message, true));
}
