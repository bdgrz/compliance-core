using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Requires the immutable intent to preserve the exact first-owner native acceptance header.</summary>
static class AccessReviewIntentBindings
{
    public static void Require(DurableIntentHeader header, OperationProvenance operation)
    {
        if (header.RootOperationId != operation.RootOperationId || header.FirstOwner != operation.FirstOwner ||
            header.AffectedOwner != operation.AffectedOwner || header.ExpectedRevisionOwner != operation.ExpectedRevisionOwner ||
            header.OriginalActor != operation.Actor || header.Episode != operation.Episode ||
            header.OriginalExpectedRevision != operation.OriginalExpectedRevision ||
            header.AuthoredIntentSha256 != operation.AuthoredIntentSha256 || header.AcceptedAt != operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("The frozen intent differs from its exact native first-owner acceptance header.");
    }
}
