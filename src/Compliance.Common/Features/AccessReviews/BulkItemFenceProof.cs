using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The cancellation proof of one item: the fence fact that refuses every delayed hold or apply of the batch.</summary>
public sealed record BulkItemFenceProof(Uuid ItemId, NativeFactReference Fence)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "bulk-access-decision-fence-proof.v1";
}
