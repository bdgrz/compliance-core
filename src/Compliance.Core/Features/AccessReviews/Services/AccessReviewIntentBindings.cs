using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Services;

/// <summary>Requires the immutable intent to preserve the exact first-owner native acceptance header.</summary>
static class AccessReviewIntentBindings
{
    public static DurableIntentHeader HeaderOf(OperationProvenance operation) =>
        new(operation.RootOperationId, operation.FirstOwner ?? operation.Owner, operation.AffectedOwner,
            operation.ExpectedRevisionOwner, operation.Actor, operation.Episode, operation.OriginalExpectedRevision,
            operation.AuthoredIntentSha256, operation.AcceptedAt);

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
