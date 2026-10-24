using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Shared pieces of the five item writes: root provenance and the append-time guard of the item owner.</summary>
static class AccessReviewItemWrites
{
    /// <summary>The provenance of a root item operation on the item's own stream.</summary>
    public static OperationProvenance Provenance(AccessReviewItemRead read, Uuid commandId, string commandKind,
        ActorReference actor, long expectedRevision, string intentDigest, DateTimeOffset now) =>
        AccessReviewEffects.Root(commandId, commandKind, read.Item.Stream, actor, expectedRevision, intentDigest, now);

    /// <summary>
    ///     The guard re-evaluated against the hydrated owner at append: closure, holds and the expected revision, and
    ///     that the owner is still at the revision the rules were decided at.
    /// </summary>
    public static Result<FirstResponseDescriptor> Prepare(AccessReviewItem owner, long expectedRevision,
        long observedRevision, FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareChange(expectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null || owner.Revision != observedRevision
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The item changed while the change was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    /// <summary>The not-found refusal every item write uses.</summary>
    public static RequestError ItemNotFound() => new(RequestErrorKind.NotFound, "The review item was not found.");
}
