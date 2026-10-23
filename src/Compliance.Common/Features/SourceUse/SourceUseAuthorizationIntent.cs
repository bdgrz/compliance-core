using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>
///     What a use fact points to: the immutable original workflow intent, the exact reservation and requirement, and
///     the grant and disposition once they exist. A settlement before any grant carries no grant and fences the key.
/// </summary>
public sealed record SourceUseAuthorizationIntent(DurableIntentHeader Header, ImmutableContentRef ParentIntent,
    ReservationKey Reservation, SourceUseRequirement Requirement, ReservationGrant? Grant,
    SourceUseSettlementDisposition? Disposition)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "source-use-authorization-intent.v1";
}
