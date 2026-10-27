using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>
///     The token a receiver issues for one granted use. <paramref name="ReservationGeneration" /> is the receiver's own
///     monotonic counter, distinct from the source generation and from any content revision. An exact retry returns the
///     original grant.
/// </summary>
public sealed record ReservationGrant(ReservationKey Key, ulong ReservationGeneration, NativeFactReference GrantEvent);
