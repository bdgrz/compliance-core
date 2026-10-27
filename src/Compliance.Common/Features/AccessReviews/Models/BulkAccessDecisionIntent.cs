using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The complete frozen terms of one bulk decision, admitted on the campaign before the batch exists.
///     <paramref name="Items" /> is the duplicate-free, complete selection in canonical item stream order, each entry a
///     <see cref="BulkAccessDecisionItemIntent" />; the shared decision and rationale are preserved exactly.
/// </summary>
public sealed record BulkAccessDecisionIntent(DurableIntentHeader Header, Uuid CampaignId, ulong CampaignGeneration,
    Uuid DecisionBatchId, ulong ExpectedCampaignRevision, string PreviewToken, ImmutableSelectionManifest Items,
    string Decision, string Rationale)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "bulk-access-decision-intent.v1";
}
