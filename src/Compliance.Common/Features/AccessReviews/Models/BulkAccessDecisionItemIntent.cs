using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     One selected item of a bulk decision, taken from the server-generated preview: the item stream, the revision
///     its head had in the preview (<paramref name="ExpectedItemRevision" />), the reviewer and reviewer episode, and
///     the manifest of <see cref="BulkItemEligibilityProof" /> that names the exact head. A changed head conflicts
///     before any decision of the batch can publish.
/// </summary>
public sealed record BulkAccessDecisionItemIntent(Uuid ItemId, EventStreamAddress ItemOwner,
    ulong ExpectedItemRevision, Uuid ReviewerMemberId, Uuid ReviewerEpisodeId,
    ImmutableSelectionManifest EligibilityProofs)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "bulk-access-decision-item-intent.v1";
}
