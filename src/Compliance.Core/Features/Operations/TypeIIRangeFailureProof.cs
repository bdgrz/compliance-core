using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Proof that the calendar refused a range: the frozen intent and the calendar authorization of another period
///     that overlapped it. The rejection receiver verifies it against the calendar before it rejects anything.
/// </summary>
public sealed record TypeIIRangeFailureProof(ImmutableContentRef Intent, NativeFactReference Conflicting)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "type-i-i-range-failure-proof.v1";
}
