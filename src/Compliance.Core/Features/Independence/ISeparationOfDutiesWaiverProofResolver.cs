using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Independence;

/// <summary>Produces and verifies immutable proofs of exact-scope separation-of-duties waivers.</summary>
public interface ISeparationOfDutiesWaiverProofResolver
{
    /// <summary>
    /// Builds the proof for exactly this scope at <paramref name="at"/>. A waiver that does not authorize it is
    /// <see cref="RequestErrorKind.Forbidden"/>; a malformed scope is <see cref="RequestErrorKind.Validation"/>.
    /// </summary>
    ValueTask<Result<SeparationOfDutiesWaiverProof>> ResolveAsync(SeparationOfDutiesWaiverProofScope scope,
        DateTimeOffset at, CancellationToken ct = default);

    /// <summary>
    /// Re-reads the waiver and its approval fact and succeeds only when every proof field reproduces the stored
    /// waiver and <paramref name="at"/> lies inside the half-open validity interval.
    /// </summary>
    ValueTask<Result> VerifyAsync(SeparationOfDutiesWaiverProof proof, DateTimeOffset at,
        CancellationToken ct = default);
}
