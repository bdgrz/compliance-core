using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>The precise local native authority against which immutable feasibility was prepared.</summary>
sealed record AdvisorPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static AdvisorPreparedCut? Capture(Aggregate owner) => owner switch
    {
        AdvisorHandoff value => new(value.Stream, value.Version, value.CommittedStreamPosition, value.ReplayCut.Digest),
        AdvisorFeedbackThread value => new(value.Stream, value.Version, value.CommittedStreamPosition, value.ReplayCut.Digest),
        _ => null,
    };

    public bool Matches(Aggregate owner) => this == Capture(owner);

    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The advisor native authority changed while immutable content was prepared.", true));
}
