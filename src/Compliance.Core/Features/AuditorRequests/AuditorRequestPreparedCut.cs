using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>The exact native cut against which one request operation prepared immutable content and response.</summary>
sealed record AuditorRequestPreparedCut(EventStreamAddress Stream, ulong Version, ulong Position, string Digest)
{
    public static AuditorRequestPreparedCut Capture(AuditorRequest owner) =>
        new(owner.Stream, owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);

    public bool Matches(AuditorRequest owner) => this == Capture(owner);

    public static Result Changed() => Result.Failure(new RequestError(RequestErrorKind.Conflict,
        "The auditor request native authority changed while immutable content was prepared.", true));
}
