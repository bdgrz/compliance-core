using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     The publication proof of one item: the hold the batch acquired and the exact provisional decision fact
///     (stream, event and position) it applied. The batch publishes only the complete set.
/// </summary>
public sealed record BulkItemDecisionProof(Uuid ItemId, NativeFactReference Hold, NativeFactReference Decision)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "bulk-access-decision-item-proof.v1";
}
