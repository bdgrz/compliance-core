using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Shared construction of the provenance and first-response guards of access review root operations.</summary>
static class AccessReviewOperation
{
    /// <summary>The episode of every first-owner access review operation.</summary>
    public const string InitialEpisode = "initial";

    /// <summary>The provenance of a root access review operation on its own owner stream.</summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        ActorReference actor, long? expectedRevision, string intentDigest, DateTimeOffset acceptedAt) => new(
        logicalId, logicalId, null, commandKind, actor, owner, InitialEpisode,
        expectedRevision is { } expected ? (ulong)expected : null, intentDigest, acceptedAt, owner, owner);

    /// <summary>Turns a passed guard and the prepared response into the first response, or the guard failure.</summary>
    public static Result<FirstResponseDescriptor> Prepare(Result guard, FirstResponseDescriptor? response,
        string changedMessage)
    {
        if (!guard.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(guard.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, changedMessage,
                true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    /// <summary>A stale owner revision as a conflict naming the current revision.</summary>
    public static Result Stale(string record, long currentRevision) => Result.Failure(
        VersionedRecordRules.StaleRevision(record, currentRevision).ToRequestError());

    public static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
