using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Shared pieces of the five item writes: root provenance and the append-time guard of the item owner.</summary>
static class AccessReviewItemWrites
{
    /// <summary>The provenance of a root item operation on the item's own stream.</summary>
    public static OperationProvenance Provenance(AccessReviewItemRead read, Uuid commandId, string commandKind,
        ActorReference actor, long expectedRevision, string intentDigest, DateTimeOffset now) =>
        AccessReviewEffects.Root(commandId, commandKind, read.Item.Stream, actor, expectedRevision, intentDigest, now);

    /// <summary>The not-found refusal every item write uses.</summary>
    public static RequestError ItemNotFound() => new(RequestErrorKind.NotFound, "The review item was not found.");
}
