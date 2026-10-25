using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>The exact native evaluation authority used to prepare one immutable result.</summary>
sealed record EvaluationPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static EvaluationPreparedCut Capture(ControlEvaluation owner) =>
        new(owner.Stream, owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);
    public bool Matches(ControlEvaluation owner) => this == Capture(owner);
    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The control evaluation authority changed while immutable content was prepared.", true));
}
