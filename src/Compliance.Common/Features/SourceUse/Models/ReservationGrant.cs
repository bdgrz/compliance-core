using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.SourceUse.Models;

/// <summary>
///     The token a receiver issues for one granted use. <paramref name="ReservationGeneration" /> is the receiver's own
///     monotonic counter, distinct from the source generation and from any content revision. An exact retry returns the
///     original grant.
/// </summary>
public sealed record ReservationGrant(ReservationKey Key, ulong ReservationGeneration, NativeFactReference GrantEvent);
