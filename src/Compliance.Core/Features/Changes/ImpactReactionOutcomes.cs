using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>
///     The two non-fatal answers a compliance-impact receiver gives a reactor: the source event is superseded and
///     is a terminal no-op, or a dependency has not caught up and the reaction must be retried with its checkpoint
///     kept. Every other failure is a real conflict and stops the reactor.
/// </summary>
public static class ImpactReactionOutcomes
{
    /// <summary>
    ///     The exact source event is proven obsolete: a newer content version, an already recorded decision or a
    ///     later assessment owns the outcome it would have produced. Retrying cannot change the answer.
    /// </summary>
    public static RequestError Superseded { get; } = new(RequestErrorKind.Conflict,
        "The source event is superseded; nothing remains to apply for it.", false);

    /// <summary>
    ///     Sends the effect of a reaction. A superseded answer ends the reaction for its exact source event and lets the
    ///     checkpoint advance; a transient answer or any other failure throws so the checkpoint is kept and the event is
    ///     replayed.
    /// </summary>
    public static async ValueTask SendAsync(IRequestBus bus, IRequest command, IReactorContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        var result = await bus.SendAsync(command, context, ct).ConfigureAwait(false);
        if (!result.IsSuccess && !IsSuperseded(result.Error!))
            throw new ReactionCommandFailedException(result.Error!);
    }

    public static async ValueTask SendAsync(IRequestBus bus, IRequest<UnitResult> command, IReactorContext context, CancellationToken ct)
    {
        var result = await bus.SendAsync(command, context, ct).ConfigureAwait(false);
        if (!result.IsSuccess && !IsSuperseded(result.Error!))
            throw new ReactionCommandFailedException(result.Error!);
    }

    /// <summary>Whether an error is the superseded answer; compared by value so it survives any transport.</summary>
    public static bool IsSuperseded(RequestError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind == Superseded.Kind && !error.IsTransient &&
               string.Equals(error.Message, Superseded.Message, StringComparison.Ordinal);
    }

    /// <summary>A dependency of the effect is not visible yet; retrying later can succeed.</summary>
    public static RequestError NotReady(string reason) => new(RequestErrorKind.Conflict, reason, true);

    /// <summary>
    ///     Classifies a description decision against the owner's retained assessments. Returns null when the decision
    ///     decides the current assessment and may be applied.
    /// </summary>
    /// <param name="requestedVersion">Content version the decision was derived for.</param>
    /// <param name="currentVersion">Current content version of the owner.</param>
    /// <param name="requestedAssessmentRetained">Whether the owner retains the exact assessment the decision names.</param>
    /// <param name="alreadyDecided">Whether the current version already has a recorded description decision.</param>
    /// <param name="decidedAt">When the source decision was made.</param>
    /// <param name="assessedAt">When the named assessment was made.</param>
    public static RequestError? ClassifyDecision(int requestedVersion, int currentVersion,
        bool requestedAssessmentRetained, bool alreadyDecided, DateTimeOffset decidedAt, DateTimeOffset assessedAt)
    {
        if (requestedVersion > currentVersion)
            return NotReady("The assessment the decision names is not visible on its owner yet.");
        if (!requestedAssessmentRetained)
            return null;
        if (requestedVersion < currentVersion || alreadyDecided || decidedAt < assessedAt)
            return Superseded;
        return null;
    }
}
